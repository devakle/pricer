using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PlaywrightExtraSharp;
using PlaywrightExtraSharp.Models;
using PlaywrightExtraSharp.Plugins.ExtraStealth;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pricer.Api.Features.ExternalProducts;

public sealed class PlaywrightSearchClient
{
    private readonly ILogger<PlaywrightSearchClient> _logger;
    private readonly PlaywrightOptions _options;
    public PlaywrightSearchClient(
        ILogger<PlaywrightSearchClient> logger,
        IOptions<PlaywrightOptions> options)
    {
        _logger = logger;
        _options = options.Value ?? new PlaywrightOptions();
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
            _logger.LogInformation("Playwright scraping disabled via configuration.");
            return Array.Empty<ExternalProductDto>();
        }

        var limit = Math.Clamp(take, 1, 100);
        var targetUrl = BuildSearchUrl(query);

        await using var browser = await new PlaywrightExtra(BrowserTypeEnum.Chromium)
            .Install()
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

        var pageTimeout = _options.TimeoutMs <= 0 ? 120_000 : _options.TimeoutMs;
        var waitTimeout = _options.WaitForSelectorMs <= 0 ? 30_000 : _options.WaitForSelectorMs;
        page.SetDefaultTimeout((int)pageTimeout);

        await page.GotoAsync(
            targetUrl);

        await page.WaitForSelectorAsync(
            "li.ui-search-layout__item",
            new PageWaitForSelectorOptions { Timeout = waitTimeout });

        for (var i = 0; i < _options.ScrollCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await page.WaitForTimeoutAsync(_options.ScrollWaitMs);
        }

        var rawItems = await ExtractFastAsync(page, cancellationToken);


        var items = new List<ExternalProductDto>();
        foreach (var item in rawItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (items.Count >= limit) break;
            if (item is null) continue;
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Link)) continue;

            var priceMoney = ParseMoney(item.Price, item.PriceCurrency);
            var originalMoney = ParseMoney(item.OriginalPrice, item.PriceCurrency);
            var discountPercent = ComputeDiscountPercent(originalMoney?.Amount, priceMoney?.Amount);

            var product = new ExternalProductDto(
                Id: item.Link,
                Title: item.Title.Trim(),
                Permalink: item.Link.Trim(),
                CanonicalUrl: item.Link.Trim(),
                CategoryPath: null,
                SearchQuery: query,
                Position: null,
                Condition: item.Condition?.Trim(),
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
                    DeliveryPromise: null,
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
                Provider: "mercadolibre",
                Source: "MercadoLibre (Playwright)",
                ScrapeProvider: "playwright",
                HtmlVersion: "ml-list-v1",
                SelectorVersion: "ml-playwright-v1",
                FetchedAtUtc: DateTimeOffset.UtcNow,
                Warnings: null,
                Location: item.Location?.Trim()
            );

            items.Add(product);
        }

        return items;
    }

    private async Task<List<PlaywrightRawItem>> ExtractFastAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var rawItemsJson = await page.EvaluateAsync<string?>("""
            () => {
                const cards = Array.from(document.querySelectorAll('li.ui-search-layout__item'));
                const items = cards.map(card => {
                    const text = (sel) => {
                        const el = card.querySelector(sel);
                        return el ? el.textContent.trim() : null;
                    };
                    const attr = (sel, name) => {
                        const el = card.querySelector(sel);
                        return el ? el.getAttribute(name) : null;
                    };
                    const title = text('.poly-component__title');
                    const link = attr('a.poly-component__title', 'href');
                    const price = text('.andes-money-amount__fraction');
                    const priceCurrency = text('.andes-money-amount__currency, .andes-money-amount__currency-symbol');
                    const originalPrice = text('.andes-money-amount--previous .andes-money-amount__fraction');
                    const condition = text('.ui-search-item__condition');
                    const location = text('.ui-search-item__location');
                    const shipping = text('.poly-component__shipping');
                    const image = attr('img', 'src') || attr('img', 'data-src');
                    return { title, link, price, priceCurrency, originalPrice, condition, location, shipping, image };
                });
                return JSON.stringify(items);
            }
        """);

            if (string.IsNullOrWhiteSpace(rawItemsJson))
            {
                return new List<PlaywrightRawItem>();
            }

            using var doc = JsonDocument.Parse(rawItemsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<PlaywrightRawItem>();
            }

            var items = new List<PlaywrightRawItem>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                items.Add(new PlaywrightRawItem(
                    GetJsonString(item, "title"),
                    GetJsonString(item, "link"),
                    GetJsonString(item, "price"),
                    GetJsonString(item, "priceCurrency"),
                    GetJsonString(item, "originalPrice"),
                    GetJsonString(item, "condition"),
                    GetJsonString(item, "location"),
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

    private static async Task<List<PlaywrightRawItem>> ExtractViaHandlesAsync(IPage page, CancellationToken cancellationToken)
    {
        var cards = await page.QuerySelectorAllAsync("li.ui-search-layout__item");
        var items = new List<PlaywrightRawItem>();
        foreach (var card in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var title = await GetInnerTextAsync(card, ".poly-component__title");
            var link = await GetAttributeAsync(card, "a.poly-component__title", "href");
            var price = await GetInnerTextAsync(card, ".andes-money-amount__fraction");
            var priceCurrency = await GetInnerTextAsync(
                card,
                ".andes-money-amount__currency, .andes-money-amount__currency-symbol");
            var originalPrice = await GetInnerTextAsync(
                card,
                ".andes-money-amount--previous .andes-money-amount__fraction");
            var condition = await GetInnerTextAsync(card, ".ui-search-item__condition");
            var location = await GetInnerTextAsync(card, ".ui-search-item__location");
            var shipping = await GetInnerTextAsync(card, ".poly-component__shipping");
            var image = await GetAttributeAsync(card, "img", "src")
                        ?? await GetAttributeAsync(card, "img", "data-src");

            items.Add(new PlaywrightRawItem(
                title,
                link,
                price,
                priceCurrency,
                originalPrice,
                condition,
                location,
                shipping,
                image));
        }

        return items;
    }

    private static string BuildSearchUrl(string query)
    {
        var slug = string.Join('-', query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return $"https://listado.mercadolibre.com.ar/{Uri.EscapeDataString(slug)}";
    }

    private sealed record PlaywrightRawItem(
        string? Title,
        string? Link,
        string? Price,
        string? PriceCurrency,
        string? OriginalPrice,
        string? Condition,
        string? Location,
        string? Shipping,
        string? Image
    );

    private static ExternalMoney? ParseMoney(string? rawAmount, string? rawCurrency)
    {
        if (string.IsNullOrWhiteSpace(rawAmount))
        {
            return null;
        }

        var digits = new string(rawAmount.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return null;
        }

        if (!decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
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
            return "ARS";
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed.Contains("US") || trimmed.Contains("U$S") || trimmed.Contains("USD"))
        {
            return "USD";
        }

        if (trimmed.Contains("$") || trimmed.Contains("ARS"))
        {
            return "ARS";
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
        return raw.Contains("gratis", StringComparison.OrdinalIgnoreCase);
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
}

public sealed class PlaywrightOptions
{
    public bool Enabled { get; init; } = true;
    public bool Headless { get; init; } = true;
    public int? TimeoutMs { get; init; } = 120_000;
    public int? WaitForSelectorMs { get; init; } = 30_000;
    public int ScrollWaitMs { get; init; } = 200;
    public int ScrollCount { get; init; } = 10;
    public float? SlowMoMs { get; init; }
    public string? UserAgent { get; init; } = "Pricer/1.0";
    public string? BrowserPath { get; init; }
    public string? DownloadPath { get; init; }
}
