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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Pricer.Api.Features.ExternalProducts;

public sealed class AmazonPlaywrightSearchClient
{
    private static readonly Regex PriceRegex = new(@"(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?)", RegexOptions.Compiled);

    private readonly ILogger<AmazonPlaywrightSearchClient> _logger;
    private readonly AmazonPlaywrightOptions _options;

    public AmazonPlaywrightSearchClient(
        ILogger<AmazonPlaywrightSearchClient> logger,
        IOptions<AmazonPlaywrightOptions> options)
    {
        _logger = logger;
        _options = options.Value ?? new AmazonPlaywrightOptions();
    }

    public async Task<IReadOnlyList<ExternalProductDto>> SearchAsync(
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ExternalProductDto>();
        }

        if (!_options.Enabled)
        {
            _logger.LogInformation("Amazon Playwright scraping disabled via configuration.");
            return Array.Empty<ExternalProductDto>();
        }

        var limit = Math.Clamp(take, 1, 100);
        var targetUrl = BuildSearchUrl(query);

        await using var browser = await new PlaywrightExtra(BrowserTypeEnum.Chromium)
            .Use(new StealthExtraPlugin())
            .LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = _options.Headless,
                    SlowMo = _options.SlowMoMs,
                    ExecutablePath = _options.BrowserPath
                },
                persistContext: true);

        var page = await browser.NewPageAsync((BrowserNewPageOptions?)null);
        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
            {
                ["User-Agent"] = _options.UserAgent!
            });
        }

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

        await TryAcceptCookiesAsync(page);

        await page.WaitForSelectorAsync(
            "div[data-component-type='s-search-result']",
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

            var link = NormalizeAmazonLink(item.Link);
            var priceMoney = ParseMoney(item.PriceWhole, item.PriceSymbol);
            var originalMoney = ParseMoney(item.OriginalPrice, item.PriceSymbol);
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
                Provider: "amazon",
                Source: "Amazon (Playwright)",
                ScrapeProvider: "playwright",
                HtmlVersion: "amz-search-v1",
                SelectorVersion: "amz-search-v1",
                FetchedAtUtc: DateTimeOffset.UtcNow,
                Warnings: null,
                Location: null
            );

            items.Add(product);
        }

        return items;
    }

    private async Task<List<AmazonRawItem>> ExtractFastAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var rawItemsJson = await page.EvaluateAsync<string?>("""
            () => {
                const cards = Array.from(document.querySelectorAll("div[data-component-type='s-search-result']"));
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
                    const title = text(card, "h2 span") || text(card, ".s-title-instructions-style h2 span");
                    const link = attr(card, "h2 a", "href") || attr(card, "a.a-link-normal", "href");
                    const priceWhole = text(card, "span.a-price > span.a-offscreen") || text(card, "span.a-price-whole");
                    const priceSymbol = text(card, "span.a-price-symbol");
                    const originalPrice = text(card, "span.a-text-price > span.a-offscreen");
                    const shipping = text(card, "[data-cy='delivery-recipe'] span")
                        || text(card, "span.a-color-base.a-text-bold")
                        || text(card, "span.a-color-secondary");
                    const image = attr(card, "img.s-image", "src") || attr(card, "img.s-image", "data-src");
                    return { title, link, priceWhole, priceSymbol, originalPrice, shipping, image };
                }).filter(item => item && item.title && item.link);
                return JSON.stringify(items);
            }
        """);

            if (string.IsNullOrWhiteSpace(rawItemsJson))
            {
                return new List<AmazonRawItem>();
            }

            using var doc = JsonDocument.Parse(rawItemsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<AmazonRawItem>();
            }

            var items = new List<AmazonRawItem>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var title = GetJsonString(item, "title");
                var link = GetJsonString(item, "link");
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                {
                    continue;
                }

                items.Add(new AmazonRawItem(
                    title,
                    link,
                    GetJsonString(item, "priceWhole"),
                    GetJsonString(item, "priceSymbol"),
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

    private static async Task<List<AmazonRawItem>> ExtractViaHandlesAsync(IPage page, CancellationToken cancellationToken)
    {
        var cards = await page.QuerySelectorAllAsync(".sg-col-4-of-24");
        var items = new List<AmazonRawItem>();
        
        foreach (var card in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var title = await GetInnerTextAsync(card, "h2 span")
                        ?? await GetInnerTextAsync(card, ".s-title-instructions-style h2 span");
            var link = await GetAttributeAsync(card, "h2 a", "href")
                       ?? await GetAttributeAsync(card, "a.a-link-normal", "href");
            var priceWhole = await GetInnerTextAsync(card, "span.a-price > span.a-offscreen")
                             ?? await GetInnerTextAsync(card, "span.a-price-whole");
            var priceSymbol = await GetInnerTextAsync(card, "span.a-price-symbol");
            var originalPrice = await GetInnerTextAsync(card, "span.a-text-price > span.a-offscreen");
            var shipping = await GetInnerTextAsync(card, "[data-cy='delivery-recipe'] span")
                           ?? await GetInnerTextAsync(card, "span.a-color-base.a-text-bold")
                           ?? await GetInnerTextAsync(card, "span.a-color-secondary");
            var image = await GetAttributeAsync(card, "img.s-image", "src")
                        ?? await GetAttributeAsync(card, "img.s-image", "data-src");

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            items.Add(new AmazonRawItem(
                title,
                link,
                priceWhole,
                priceSymbol,
                originalPrice,
                shipping,
                image
            ));
        }

        return items;
    }

    private static string BuildSearchUrl(string query)
    {
        var encoded = Uri.EscapeDataString(query);
        return $"https://www.amazon.com/s?k={encoded}";
    }

    private async Task TryAcceptCookiesAsync(IPage page)
    {
        try
        {
            await page.ClickAsync("#sp-cc-accept", new PageClickOptions { Timeout = 2_000 });
        }
        catch
        {
            // Ignore if cookie banner not present.
        }
    }

    private static string NormalizeAmazonLink(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw ?? string.Empty;
        if (raw.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return raw;
        return $"https://www.amazon.com{raw}";
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
        var normalized = raw.Replace(".", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var currency = NormalizeCurrency(rawCurrency);
        return new ExternalMoney(currency, amount);
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

        if (trimmed.Contains("$"))
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

    private static async Task<string?> GetInnerTextAsync(IElementHandle card, string selector)
    {
        var element = await card.QuerySelectorAsync(selector);
        if (element is null) return null;
        var text = await element.InnerTextAsync();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static async Task<string?> GetAttributeAsync(IElementHandle card, string selector, string attribute)
    {
        var element = await card.QuerySelectorAsync(selector);
        if (element is null) return null;
        var value = await element.GetAttributeAsync(attribute);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record AmazonRawItem(
        string? Title,
        string? Link,
        string? PriceWhole,
        string? PriceSymbol,
        string? OriginalPrice,
        string? Shipping,
        string? Image
    );
}

public sealed class AmazonPlaywrightOptions
{
    public bool Enabled { get; init; } = true;
    public bool Headless { get; init; } = true;
    public int? TimeoutMs { get; init; } = 120_000;
    public int? WaitForSelectorMs { get; init; } = 30_000;
    public int ScrollWaitMs { get; init; } = 200;
    public int ScrollCount { get; init; } = 2;
    public float? SlowMoMs { get; init; }
    public string? UserAgent { get; init; } = "Pricer/1.0";
    public string? BrowserPath { get; init; }
}
