using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PlaywrightExtraSharp;
using PlaywrightExtraSharp.Models;
using PlaywrightExtraSharp.Plugins.ExtraStealth;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Pricer.Api.Features.ExternalProducts;

public sealed class AliExpressPlaywrightSearchClient
{
    private static readonly Regex PriceRegex = new(@"(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?)", RegexOptions.Compiled);
    private static readonly Regex ItemLinkRegex = new(@"/item/\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILogger<AliExpressPlaywrightSearchClient> _logger;
    private readonly AliExpressPlaywrightOptions _options;

    public AliExpressPlaywrightSearchClient(
        ILogger<AliExpressPlaywrightSearchClient> logger,
        IOptions<AliExpressPlaywrightOptions> options)
    {
        _logger = logger;
        _options = options.Value ?? new AliExpressPlaywrightOptions();
    }

    public async Task<IReadOnlyList<ExternalProductDto>> SearchAsync(
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        var auth = "brd-customer-hl_fc00446d-zone-datacenter_proxy1-country-ar:w29y4ddomapp";
        var endpointUrl = $"wss://{auth}@brd.superproxy.io:9222";

        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ExternalProductDto>();
        }

        if (!_options.Enabled)
        {
            _logger.LogInformation("AliExpress Playwright scraping disabled via configuration.");
            return Array.Empty<ExternalProductDto>();
        }

        var limit = Math.Clamp(take, 1, 100);
        var targetUrl = BuildSearchUrl(query);

        await using var browser = await new PlaywrightExtra(BrowserTypeEnum.Chromium)
            .ConnectOverCDPAsync(endpointUrl);

        var page = await browser.NewPageAsync((BrowserNewPageOptions?)null);
        // if (!string.IsNullOrWhiteSpace(_options.UserAgent))
        // {
        //     await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        //     {
        //         ["User-Agent"] = _options.UserAgent!
        //     });
        // }
        var cdpSession = await page.Context.NewCDPSessionAsync(page);

        var pageTimeout = _options.TimeoutMs.GetValueOrDefault(120_000);
        if (pageTimeout <= 0) pageTimeout = 120_000;
        var waitTimeout = _options.WaitForSelectorMs.GetValueOrDefault(30_000);
        if (waitTimeout <= 0) waitTimeout = 30_000;
        page.SetDefaultTimeout(pageTimeout);

        await page.GotoAsync(
            targetUrl,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = pageTimeout
            });

        // Wait for CAPTCHA to be solved (auto-solver runs in the background)
        var result = await cdpSession.SendAsync(
            "Captcha.solve",
            new Dictionary<string, object>
            {
                ["detectTimeout"] = 30 * 1000 // 30 seconds to detect a CAPTCHA
            }
        );
        var status = result.Value.GetProperty("status").GetString();
        Console.WriteLine($"Captcha solve status: {status}");

        await page.WaitForSelectorAsync(
            ".search-item-card-wrapper-gallery",
            new PageWaitForSelectorOptions { Timeout = waitTimeout });

        var scrollCount = _options.ScrollCount <= 0 ? 2 : _options.ScrollCount;
        var scrollWait = _options.ScrollWaitMs <= 0 ? 200 : _options.ScrollWaitMs;
        for (var i = 0; i < scrollCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await page.WaitForTimeoutAsync(scrollWait);
        }

        var rawItems = await ExtractFastAsync(page, cancellationToken);

        var items = new List<ExternalProductDto>();
        foreach (var item in rawItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (items.Count >= limit) break;
            if (item is null) continue;
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Link)) continue;
            if (!ItemLinkRegex.IsMatch(item.Link)) continue;

            var link = NormalizeAliExpressLink(item.Link);
            var priceMoney = ParseMoney(item.Price, item.PriceCurrency);
            var originalMoney = ParseMoney(item.OriginalPrice, item.PriceCurrency);
            var discountPercent = ComputeDiscountPercent(originalMoney?.Amount, priceMoney?.Amount);

            var product = new ExternalProductDto(
                Id: link,
                Title: item.Title.Trim(),
                Permalink: link,
                CanonicalUrl: link,
                CategoryPath: null,
                SearchQuery: query,
                Position: null,
                Condition: null,
                Availability: null,
                SoldQuantity: null,
                LastUpdated: null,
                Offer: new ExternalProductOffer(
                    Price: priceMoney,
                    OriginalPrice: originalMoney,
                    DiscountPercent: discountPercent,
                    Installments: null,
                    PricePerUnit: null,
                    PaymentBadges: null
                ),
                Shipping: new ExternalShippingInfo(
                    FreeShipping: InferFreeShipping(item.Shipping),
                    ShippingMode: null,
                    DeliveryPromise: item.Shipping?.Trim(),
                    PickupAvailable: null
                ),
                Seller: new ExternalSellerInfo(
                    SellerId: null,
                    Name: null,
                    OfficialStore: null,
                    SellerType: null,
                    ReputationLevel: null,
                    Badges: null
                ),
                Media: new ExternalProductMedia(
                    ThumbnailUrl: item.Image,
                    ImageUrls: string.IsNullOrWhiteSpace(item.Image) ? null : new[] { item.Image },
                    VideoUrls: Array.Empty<string>()
                ),
                Attributes: null,
                Provider: "aliexpress",
                Source: "AliExpress (Playwright)",
                ScrapeProvider: "playwright",
                HtmlVersion: "ae-search-v1",
                SelectorVersion: "ae-search-v1",
                FetchedAtUtc: DateTimeOffset.UtcNow,
                Warnings: null,
                Location: null
            );

            items.Add(product);
        }

        return items;
    }

    private async Task<List<AliExpressRawItem>> ExtractFastAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var rawItemsJson = await page.EvaluateAsync<string?>("""
            () => {
                const cards = Array.from(document.querySelectorAll(".search-item-card-wrapper-gallery"));
                const text = (root, sel) => {
                    const el = root.querySelector(sel);
                    if (!el) return null;
                    const value = el.textContent ? el.textContent.trim() : null;
                    return value || null;
                };
                const attr = (root, sel, name) => {
                    const el = root.querySelector(sel);
                    if (!el) return null;
                    const value = el.getAttribute(name);
                    return value ? value.trim() : null;
                };
                const items = cards.map(card => {
                    const link = attr(card, "a.search-card-item", "href")
                        || attr(card, "a[href*='/item/']", "href")
                        || attr(card, "a", "href");
                    let title = text(card, ".k7_af")
                        || text(card, ".k7_kf")
                        || attr(card, ".k7_af", "title")
                        || attr(card, ".k7_af", "aria-label");
                    const price = text(card, ".k7_c6 .k7_lw")
                        || text(card, ".k7_c6");
                    const originalPrice = text(card, ".k7_lx")
                        || text(card, ".k7_lx span");
                    const shipping = text(card, ".k7_l7")
                        || text(card, "[class*='shipping']")
                        || text(card, "[class*='delivery']")
                        || text(card, "[class*='logistics']");
                    const image = attr(card, "img.nj_bm", "src")
                        || attr(card, "img", "src")
                        || attr(card, "img", "data-src")
                        || attr(card, "img", "data-lazy-src");
                    if (!title) {
                        title = attr(card, "img.nj_bm", "alt")
                            || attr(card, "img", "alt");
                    }
                    return { title, link, price, priceCurrency: null, originalPrice, shipping, image };
                }).filter(item => item && item.link);
                return JSON.stringify(items);
            }
        """);
            if (string.IsNullOrWhiteSpace(rawItemsJson))
            {
                return new List<AliExpressRawItem>();
            }

            using var doc = JsonDocument.Parse(rawItemsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<AliExpressRawItem>();
            }

            var items = new List<AliExpressRawItem>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var title = GetJsonString(item, "title");
                var link = GetJsonString(item, "link");
                if (string.IsNullOrWhiteSpace(link)) continue;
                items.Add(new AliExpressRawItem(
                    title,
                    link,
                    GetJsonString(item, "price"),
                    GetJsonString(item, "priceCurrency"),
                    GetJsonString(item, "originalPrice"),
                    GetJsonString(item, "shipping"),
                    GetJsonString(item, "image")
                ));
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fast extraction failed. Falling back to per-card scraping.");
            return await ExtractViaHandlesAsync(page, cancellationToken);
        }
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static async Task<List<AliExpressRawItem>> ExtractViaHandlesAsync(IPage page, CancellationToken cancellationToken)
    {
        var cards = await page.QuerySelectorAllAsync(".search-item-card-wrapper-gallery");
        var items = new List<AliExpressRawItem>();
        foreach (var card in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var link = await GetAttributeAsync(card, "a.search-card-item", "href")
                       ?? await GetAttributeAsync(card, "a[href*='/item/']", "href")
                       ?? await GetAttributeAsync(card, "a", "href");
            var title = await GetInnerTextAsync(card, ".k7_af")
                        ?? await GetInnerTextAsync(card, ".k7_kf")
                        ?? await GetAttributeAsync(card, ".k7_af", "title")
                        ?? await GetAttributeAsync(card, ".k7_af", "aria-label");
            var price = await GetInnerTextAsync(card, ".k7_c6 .k7_lw")
                        ?? await GetInnerTextAsync(card, ".k7_c6");
            var originalPrice = await GetInnerTextAsync(card, ".k7_lx")
                               ?? await GetInnerTextAsync(card, ".k7_lx span");
            var shipping = await GetInnerTextAsync(card, ".k7_l7")
                           ?? await GetInnerTextAsync(card, "[class*='shipping']")
                           ?? await GetInnerTextAsync(card, "[class*='delivery']")
                           ?? await GetInnerTextAsync(card, "[class*='logistics']");
            var image = await GetAttributeAsync(card, "img.nj_bm", "src")
                        ?? await GetAttributeAsync(card, "img", "src")
                        ?? await GetAttributeAsync(card, "img", "data-src")
                        ?? await GetAttributeAsync(card, "img", "data-lazy-src");

            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = await GetAttributeAsync(card, "img.nj_bm", "alt")
                        ?? await GetAttributeAsync(card, "img", "alt");
            }

            items.Add(new AliExpressRawItem(
                title,
                link,
                price,
                null,
                originalPrice,
                shipping,
                image
            ));
        }

        return items;
    }

    private static async Task<string?> GetInnerTextAsync(IElementHandle card, string selector)
    {
        var element = await card.QuerySelectorAsync(selector);
        if (element is null) return null;
        var text = await element.InnerTextAsync();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static async Task<string?> GetAttributeAsync(IElementHandle card, string selector, string attribute)
    {
        var element = await card.QuerySelectorAsync(selector);
        if (element is null) return null;
        var value = await element.GetAttributeAsync(attribute);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildSearchUrl(string query)
    {
        var encoded = Uri.EscapeDataString(query);
        return $"https://www.aliexpress.com/wholesale?SearchText={encoded}";
    }
    private static string NormalizeAliExpressLink(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw ?? string.Empty;
        if (raw.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return raw;
        if (raw.StartsWith("//")) return $"https:{raw}";
        return $"https://www.aliexpress.com{raw}";
    }

    private static ExternalMoney? ParseMoney(string? rawAmount, string? rawCurrency)
    {
        if (string.IsNullOrWhiteSpace(rawAmount))
        {
            return null;
        }

        var match = PriceRegex.Match(rawAmount);
        if (!match.Success) return null;

        var raw = match.Groups[1].Value;
        var normalized = NormalizeNumber(raw);
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var currency = NormalizeCurrency(rawCurrency ?? rawAmount);
        return new ExternalMoney(currency, amount);
    }

    private static string NormalizeNumber(string raw)
    {
        var lastDot = raw.LastIndexOf('.');
        var lastComma = raw.LastIndexOf(',');

        if (lastDot > -1 && lastComma > -1)
        {
            if (lastDot > lastComma)
            {
                raw = raw.Replace(",", string.Empty);
            }
            else
            {
                raw = raw.Replace(".", string.Empty).Replace(",", ".");
            }
        }
        else if (lastComma > -1)
        {
            var digitsAfter = raw.Length - lastComma - 1;
            raw = digitsAfter == 2 ? raw.Replace(",", ".") : raw.Replace(",", string.Empty);
        }
        else if (lastDot > -1)
        {
            var digitsAfter = raw.Length - lastDot - 1;
            if (digitsAfter != 2)
            {
                raw = raw.Replace(".", string.Empty);
            }
        }

        return raw;
    }

    private static string NormalizeCurrency(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "USD";
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed.Contains("US") || trimmed.Contains("USD"))
        {
            return "USD";
        }

        if (trimmed.Contains("$") || trimmed.Contains("US$"))
        {
            return "USD";
        }

        return trimmed;
    }

    private static int? ComputeDiscountPercent(decimal? original, decimal? current)
    {
        if (!original.HasValue || !current.HasValue) return null;
        if (original.Value <= 0 || current.Value >= original.Value) return null;

        var percent = (int)Math.Round((1 - (current.Value / original.Value)) * 100, MidpointRounding.AwayFromZero);
        return percent <= 0 ? null : percent;
    }

    private static bool? InferFreeShipping(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Contains("free", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AliExpressRawItem(
        string? Title,
        string? Link,
        string? Price,
        string? PriceCurrency,
        string? OriginalPrice,
        string? Shipping,
        string? Image
    );
}

public sealed class AliExpressPlaywrightOptions
{
    public bool Enabled { get; init; } = true;
    public bool Headless { get; init; } = true;
    public int? TimeoutMs { get; init; } = 120_000;
    public int? WaitForSelectorMs { get; init; } = 30_000;
    public int ScrollWaitMs { get; init; } = 200;
    public int ScrollCount { get; init; } = 2;
    public float? SlowMoMs { get; init; }
    public string? UserAgent { get; init; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
    public string? BrowserPath { get; init; }
}
