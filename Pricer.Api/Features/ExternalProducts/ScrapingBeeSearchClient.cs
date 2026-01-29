using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Pricer.Api.Features.ExternalProducts;

public sealed class ScrapingBeeSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ScrapingBeeSearchClient> _logger;
    private readonly ScrapingBeeOptions _options;

    public ScrapingBeeSearchClient(
        HttpClient httpClient,
        ILogger<ScrapingBeeSearchClient> logger,
        IOptions<ScrapingBeeOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value ?? new ScrapingBeeOptions();
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

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("ScrapingBee ApiKey faltante. No se ejecuta scraping.");
            return Array.Empty<ExternalProductDto>();
        }

        var limit = Math.Clamp(take, 1, 100);
        var targetUrl = BuildSearchUrl(query);
        var rulesJson = BuildExtractRulesJson();

        var response = await FetchExtractedAsync(targetUrl, rulesJson, _options.RenderJs, cancellationToken);
        var items = MapItems(response, query, DateTimeOffset.UtcNow);

        if (items.Count == 0 && _options.JsFallback && !_options.RenderJs)
        {
            var fallback = await FetchExtractedAsync(targetUrl, rulesJson, true, cancellationToken);
            items = MapItems(fallback, query, DateTimeOffset.UtcNow);
        }

        if (items.Count > limit)
        {
            items = items.Take(limit).ToList();
        }

        return items;
    }

    private async Task<ScrapingBeeExtractResponse?> FetchExtractedAsync(
        string targetUrl,
        string rulesJson,
        bool renderJs,
        CancellationToken cancellationToken)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["api_key"] = _options.ApiKey,
            ["url"] = targetUrl
        };

        var parameters = new Dictionary<string, string>
        {
            ["render_js"] = renderJs ? "true" : "false",
            ["extract_rules"] = rulesJson
        };

        if (_options.WaitMs.HasValue)
        {
            parameters["wait"] = _options.WaitMs.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (_options.PremiumProxy)
        {
            parameters["premium_proxy"] = "true";
        }

        if (_options.StealthProxy)
        {
            parameters["stealth_proxy"] = "false";
        }

        if (!string.IsNullOrWhiteSpace(_options.CountryCode))
        {
            parameters["country_code"] = _options.CountryCode;
        }

        foreach (var kvp in parameters)
        {
            queryParams[kvp.Key] = kvp.Value;
        }

        var queryString = await new FormUrlEncodedContent(queryParams).ReadAsStringAsync(cancellationToken);
        var requestUri = $"api/v1?{queryString}";
        var fullRequestUri = _httpClient.BaseAddress != null
            ? new Uri(_httpClient.BaseAddress, requestUri).ToString()
            : requestUri;
        var curl = $"curl -X GET \"{fullRequestUri}\"";
        _logger.LogInformation("ScrapingBee request uri: {RequestUri}", fullRequestUri);
        _logger.LogInformation("ScrapingBee curl: {Curl}", curl);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "ScrapingBee error {StatusCode}: {Body}",
                (int)response.StatusCode,
                errorBody
            );
            response.EnsureSuccessStatusCode();
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<ScrapingBeeExtractResponse>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
    }

    private static string BuildSearchUrl(string query)
    {
        var slug = string.Join('-', query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return $"https://listado.mercadolibre.com.ar/{Uri.EscapeDataString(slug)}";
    }

    private static string BuildExtractRulesJson()
    {
        var rules = new
        {
            items = new
            {
                selector = "li.ui-search-layout__item", // each product card
                type = "list",
                output = new
                {
                    title = new
                    {
                        selector = ".poly-component__title",
                        output = "text"
                    },
                    link = new
                    {
                        selector = "a.poly-component__title",
                        output = "@href"
                    },
                    price = new
                    {
                        selector = ".andes-money-amount__fraction",
                        output = "text"
                    },
                    price_currency = new
                    {
                        selector = ".andes-money-amount__currency",
                        output = "text"
                    },
                    original_price = new
                    {
                        selector = ".andes-money-amount--previous .andes-money-amount__fraction",
                        output = "text"
                    },
                    condition = new
                    {
                        selector = ".ui-search-item__condition",
                        output = "text"
                    },
                    location = new
                    {
                        selector = ".ui-search-item__location",
                        output = "text"
                    },
                    shipping = new
                    {
                        selector = ".poly-component__shipping",
                        output = "text"
                    },
                    image = new
                    {
                        selector = "img",
                        output = "@src"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(rules);
    }


    private static List<ExternalProductDto> MapItems(
        ScrapingBeeExtractResponse? response,
        string query,
        DateTimeOffset fetchedAt)
    {
        if (response?.Items == null || response.Items.Count == 0)
        {
            return new List<ExternalProductDto>();
        }

        var items = new List<ExternalProductDto>();

        foreach (var item in response.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Link) || string.IsNullOrWhiteSpace(item.Title))
            {
                continue;
            }

            var price = ParseMoney(item.Price, item.PriceCurrency);
            var originalPrice = ParseMoney(item.OriginalPrice, item.PriceCurrency);
            var discountPercent = ComputeDiscountPercent(originalPrice?.Amount, price?.Amount);
            var image = string.IsNullOrWhiteSpace(item.Image) ? item.ImageFallback : item.Image;

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
                    Price: price,
                    OriginalPrice: originalPrice,
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
                    ThumbnailUrl: image,
                    ImageUrls: string.IsNullOrWhiteSpace(image) ? null : new[] { image },
                    VideoUrls: Array.Empty<string>()
                ),
                Attributes: null,
                Provider: "mercadolibre",
                Source: "MercadoLibre (ScrapingBee)",
                ScrapeProvider: "scrapingbee",
                HtmlVersion: "ml-list-v1",
                SelectorVersion: "ml-list-v1",
                FetchedAtUtc: fetchedAt,
                Warnings: null,
                Location: item.Location?.Trim()
            );

            items.Add(product);
        }

        return items;
    }

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

    private sealed record ScrapingBeeExtractResponse(
        [property: JsonPropertyName("items")] List<ScrapingBeeItem>? Items
    );

    private sealed record ScrapingBeeItem(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("link")] string? Link,
        [property: JsonPropertyName("price")] string? Price,
        [property: JsonPropertyName("price_currency")] string? PriceCurrency,
        [property: JsonPropertyName("original_price")] string? OriginalPrice,
        [property: JsonPropertyName("condition")] string? Condition,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("shipping")] string? Shipping,
        [property: JsonPropertyName("image")] string? Image,
        [property: JsonPropertyName("image_fallback")] string? ImageFallback
    );
}

public sealed class ScrapingBeeOptions
{
    public string? ApiKey { get; init; }
    public bool RenderJs { get; init; } = true;
    public bool JsFallback { get; init; } = true;
    public bool PremiumProxy { get; init; } = false;
    public bool StealthProxy { get; init; } = false;
    public string? CountryCode { get; init; } = "AR";
    public int? WaitMs { get; init; } = 2000;
}
