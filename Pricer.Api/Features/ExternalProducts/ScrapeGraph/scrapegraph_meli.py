import argparse
import asyncio
import json
import os
import sys
from typing import List, Optional

from dotenv import load_dotenv
from pydantic import BaseModel, Field
from playwright.async_api import async_playwright

from scrapegraphai.graphs import OmniScraperGraph


class MeliItem(BaseModel):
    title: str = Field(description="Product title")
    link: str = Field(description="Product URL")
    price: Optional[str] = Field(default=None, description="Current price text")
    price_currency: Optional[str] = Field(default=None, description="Currency text")
    original_price: Optional[str] = Field(default=None, description="Original price text")
    condition: Optional[str] = Field(default=None, description="Item condition")
    location: Optional[str] = Field(default=None, description="Seller location")
    shipping: Optional[str] = Field(default=None, description="Shipping info")
    image: Optional[str] = Field(default=None, description="Image URL")


class MeliItemsResponse(BaseModel):
    items: List[MeliItem]


def build_search_url(query: str) -> str:
    slug = "-".join(part for part in query.split() if part.strip())
    return f"https://listado.mercadolibre.com.ar/{slug}"


def parse_bool(val: str) -> bool:
    return str(val).strip().lower() in ("1", "true", "yes", "y", "on")


async def get_html_after_scroll(
    url: str,
    headless: bool = False,
    max_scrolls: int = 25,
    scroll_pause_ms: int = 900,
    network_idle_ms: int = 1200,
) -> str:
    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=headless)
        context = await browser.new_context(
            user_agent=(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                "AppleWebKit/537.36 (KHTML, like Gecko) "
                "Chrome/120.0.0.0 Safari/537.36"
            ),
            viewport={"width": 1366, "height": 900},
        )
        page = await context.new_page()

        await page.goto(url, wait_until="domcontentloaded", timeout=120_000)
        await page.wait_for_timeout(800)

        prev_count = -1
        stable_loops = 0

        for _ in range(max_scrolls):
            count = await page.locator("li.ui-search-layout__item").count()

            # stop if it isn't growing for 2 loops
            if count == prev_count:
                stable_loops += 1
            else:
                stable_loops = 0

            if stable_loops >= 2:
                break

            prev_count = count

            await page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
            await page.wait_for_timeout(scroll_pause_ms)
            await page.wait_for_timeout(network_idle_ms)

        # final nudge
        await page.evaluate("window.scrollTo(0, document.body.scrollHeight - 500)")
        await page.wait_for_timeout(600)

        # Debug: print how many cards we actually loaded in the DOM
        final_count = await page.locator("li.ui-search-layout__item").count()
        print(f"[playwright] ui-search-layout__item count in DOM: {final_count}")

        html = await page.content()
        await context.close()
        await browser.close()
        return html


def run_graph(api_key: str, model: str, headless: bool, prompt: str, source: str) -> dict:
    graph_config = {
        "llm": {
            "api_key": api_key,
            "model": model,
        },
        "verbose": True,
        "headless": headless,
    }

    graph = OmniScraperGraph(
        prompt=prompt,
        source=source,  # URL or HTML
        config=graph_config,
        schema=MeliItemsResponse,
    )

    return graph.run()


def main() -> int:
    load_dotenv()
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

    parser = argparse.ArgumentParser()
    parser.add_argument("--query", required=True)
    parser.add_argument("--take", type=int, default=20)  # not enforced by prompt; used for trimming output
    parser.add_argument("--model", default="openai/gpt-4o-mini")
    parser.add_argument("--headless", default="true")
    parser.add_argument("--timeout", type=int, default=120)

    # New functionality
    parser.add_argument("--scroll", default="false", help="Enable Playwright scrolling before extraction")
    parser.add_argument("--scrolls", type=int, default=20, help="Max number of scrolls (if --scroll=true)")

    args = parser.parse_args()

    api_key = os.getenv("OPENAI_API_KEY")
    if not api_key:
        sys.stderr.write("OPENAI_API_KEY is not set.\n")
        return 2

    take = max(1, min(args.take, 200))
    headless = False
    do_scroll = parse_bool(args.scroll)

    timeout_ms = max(5, args.timeout) * 1000

    source_url = build_search_url(args.query)

    prompt = "Extract all products."


    html = asyncio.run(
        get_html_after_scroll(
            url=source_url,
            headless=headless,
            max_scrolls=max(1, min(args.scrolls, 200)),
        )
    )

    result = run_graph(api_key, args.model, headless, prompt, html)


    # # Trim result to --take after extraction (prompt stays simple)
    # if isinstance(result, dict) and isinstance(result.get("items"), list):
    #     result["items"] = result["items"][:take]

    sys.stdout.write(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
