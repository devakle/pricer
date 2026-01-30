using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Pricer.Api.Common.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pricer.Api.Features.ExternalProducts;

[ApiController]
[Route("api/external-products")]
public sealed class ExternalProductsController : ControllerBase
{
    private readonly ScrapingBeeSearchClient _scrapingBeeSearchClient;
    private readonly ScrapeGraphSearchClient _scrapeGraphSearchClient;
    private readonly ScrapeGraphOptions _scrapeGraphOptions;
    private readonly PlaywrightSearchClient _playwrightSearchClient;
    private readonly PlaywrightOptions _playwrightOptions;
    private readonly AmazonPlaywrightSearchClient _amazonPlaywrightSearchClient;
    private readonly AmazonPlaywrightOptions _amazonPlaywrightOptions;
    private readonly AliExpressPlaywrightSearchClient _aliExpressPlaywrightSearchClient;
    private readonly AliExpressPlaywrightOptions _aliExpressPlaywrightOptions;
    private readonly IDistributedCache _cache;
    private readonly ExternalSearchCacheOptions _cacheOptions;

    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);

    public ExternalProductsController(
        ScrapingBeeSearchClient scrapingBeeSearchClient,
        ScrapeGraphSearchClient scrapeGraphSearchClient,
        IOptions<ScrapeGraphOptions> scrapeGraphOptions,
        PlaywrightSearchClient playwrightSearchClient,
        IOptions<PlaywrightOptions> playwrightOptions,
        AmazonPlaywrightSearchClient amazonPlaywrightSearchClient,
        IOptions<AmazonPlaywrightOptions> amazonPlaywrightOptions,
        AliExpressPlaywrightSearchClient aliExpressPlaywrightSearchClient,
        IOptions<AliExpressPlaywrightOptions> aliExpressPlaywrightOptions,
        IDistributedCache cache,
        IOptions<ExternalSearchCacheOptions> cacheOptions)
    {
        _scrapingBeeSearchClient = scrapingBeeSearchClient;
        _scrapeGraphSearchClient = scrapeGraphSearchClient;
        _scrapeGraphOptions = scrapeGraphOptions.Value ?? new ScrapeGraphOptions();
        _playwrightSearchClient = playwrightSearchClient;
        _playwrightOptions = playwrightOptions.Value ?? new PlaywrightOptions();
        _amazonPlaywrightSearchClient = amazonPlaywrightSearchClient;
        _amazonPlaywrightOptions = amazonPlaywrightOptions.Value ?? new AmazonPlaywrightOptions();
        _aliExpressPlaywrightSearchClient = aliExpressPlaywrightSearchClient;
        _aliExpressPlaywrightOptions = aliExpressPlaywrightOptions.Value ?? new AliExpressPlaywrightOptions();
        _cache = cache;
        _cacheOptions = cacheOptions.Value ?? new ExternalSearchCacheOptions();
    }

    [Authorize(Policy = "UserOnly")]
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] string? provider,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0 || take > 100) take = 20;

        var normalizedProvider = string.IsNullOrWhiteSpace(provider)
            ? null
            : provider.Trim().ToLowerInvariant();

        var rawQuery = string.IsNullOrWhiteSpace(query)
            ? null
            : query.Trim();

        var normalizedQuery = string.IsNullOrWhiteSpace(rawQuery)
            ? null
            : rawQuery.ToLowerInvariant();

        if (normalizedProvider is not null
            && normalizedProvider is not ("all" or "mercadolibre" or "mercadolibre-playwright" or "playwright" or "amazon" or "amazon-playwright" or "aliexpress" or "aliexpress-playwright" or "local"))
        {
            return BadRequest(ApiResponse<IReadOnlyList<ExternalProductDto>>.Failure(
                "provider_not_supported",
                "Proveedor no soportado."
            ));
        }

        var cacheKey = BuildCacheKey(normalizedProvider, normalizedQuery, take);
        if (ShouldUseCache(normalizedQuery))
        {
            var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var cachedItems = JsonSerializer.Deserialize<List<ExternalProductDto>>(cached, CacheJsonOptions)
                                 ?? new List<ExternalProductDto>();
                return Ok(ApiResponse<IReadOnlyList<ExternalProductDto>>.Success(cachedItems));
            }
        }

        var data = new List<ExternalProductDto>();

        try
        {
            if (normalizedProvider is null or "all" or "mercadolibre" or "mercadolibre-playwright" or "playwright")
            {
                if (!string.IsNullOrWhiteSpace(rawQuery))
                {
                    IReadOnlyList<ExternalProductDto> mercadoLibreItems;
                    if (normalizedProvider is "mercadolibre-playwright" or "playwright")
                    {
                        mercadoLibreItems = await _playwrightSearchClient.SearchAsync(
                            rawQuery,
                            take,
                            cancellationToken
                        );
                    }
                    else if (_playwrightOptions.Enabled)
                    {
                        mercadoLibreItems = await _playwrightSearchClient.SearchAsync(
                            rawQuery,
                            take,
                            cancellationToken
                        );

                    }
                    else
                    {
                        mercadoLibreItems = await _scrapingBeeSearchClient.SearchAsync(
                            rawQuery,
                            take,
                            cancellationToken
                        );
                    }
                    data.AddRange(mercadoLibreItems);
                }
                else if (normalizedProvider == "mercadolibre")
                {
                    return BadRequest(ApiResponse<IReadOnlyList<ExternalProductDto>>.Failure(
                        "query_required",
                        "La busqueda de MercadoLibre requiere un termino."
                    ));
                }
            }

            if (normalizedProvider is null or "all" or "amazon" or "amazon-playwright")
            {
                if (!string.IsNullOrWhiteSpace(rawQuery))
                {
                    if (_amazonPlaywrightOptions.Enabled)
                    {
                        var amazonItems = await _amazonPlaywrightSearchClient.SearchAsync(
                            rawQuery,
                            take,
                            cancellationToken
                        );
                        data.AddRange(amazonItems);
                    }
                }
                else if (normalizedProvider is "amazon" or "amazon-playwright")
                {
                    return BadRequest(ApiResponse<IReadOnlyList<ExternalProductDto>>.Failure(
                        "query_required",
                        "La busqueda de Amazon requiere un termino."
                    ));
                }
            }

            if (normalizedProvider is null or "all" or "aliexpress" or "aliexpress-playwright")
            {
                if (!string.IsNullOrWhiteSpace(rawQuery))
                {
                    if (_aliExpressPlaywrightOptions.Enabled)
                    {
                        var aliExpressItems = await _aliExpressPlaywrightSearchClient.SearchAsync(
                            rawQuery,
                            take,
                            cancellationToken
                        );
                        data.AddRange(aliExpressItems);
                    }
                }
                else if (normalizedProvider is "aliexpress" or "aliexpress-playwright")
                {
                    return BadRequest(ApiResponse<IReadOnlyList<ExternalProductDto>>.Failure(
                        "query_required",
                        "La busqueda de AliExpress requiere un termino."
                    ));
                }
            }

            if (normalizedProvider is null or "all" or "local")
            {
                var localItems = BuildLocalSampleProducts(normalizedQuery);
                data.AddRange(localItems);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                ApiResponse<IReadOnlyList<ExternalProductDto>>.Failure(
                    "external_provider_error",
                    ex.Message
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            data = data
                .Where(p => p.Title.ToLowerInvariant().Contains(normalizedQuery))
                .ToList();
        }

        data = data.Take(take).ToList();

        var withPositions = data
            .Select((item, index) => item with
            {
                Position = index + 1,
                SearchQuery = query
            })
            .ToList();

        if (ShouldUseCache(normalizedQuery))
        {
            var ttlSeconds = _cacheOptions.TtlSeconds <= 0 ? 300 : _cacheOptions.TtlSeconds;
            var payload = JsonSerializer.Serialize(withPositions, CacheJsonOptions);
            await _cache.SetStringAsync(
                cacheKey,
                payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                },
                cancellationToken);
        }

        return Ok(ApiResponse<IReadOnlyList<ExternalProductDto>>.Success(withPositions));
    }

    private bool ShouldUseCache(string? normalizedQuery)
    {
        if (!_cacheOptions.Enabled) return false;
        return !string.IsNullOrWhiteSpace(normalizedQuery);
    }

    private static string BuildCacheKey(string? provider, string? normalizedQuery, int take)
    {
        var safeProvider = string.IsNullOrWhiteSpace(provider) ? "all" : provider;
        var safeQuery = string.IsNullOrWhiteSpace(normalizedQuery) ? "all" : normalizedQuery;
        return $"ext-products:{safeProvider}:{safeQuery}:{take}";
    }

    private static List<ExternalProductDto> BuildLocalSampleProducts(string? query)
    {
        var fetchedAt = DateTimeOffset.UtcNow;
        var items = new List<ExternalProductDto>
        {
            new(
                Id: "LOCAL-001",
                Title: "Arroz largo fino 1kg",
                Permalink: "https://local.pricer.app/products/arroz-1kg",
                CanonicalUrl: "https://local.pricer.app/products/arroz-1kg",
                CategoryPath: new[] { "Alimentos", "Granos" },
                SearchQuery: query,
                Position: null,
                Condition: "Nuevo",
                Availability: "En stock",
                SoldQuantity: null,
                LastUpdated: fetchedAt.AddHours(-6),
                Offer: new ExternalProductOffer(
                    Price: new ExternalMoney("ARS", 1490),
                    OriginalPrice: null,
                    DiscountPercent: null,
                    Installments: null,
                    PricePerUnit: "ARS 1490 / kg",
                    PaymentBadges: null
                ),
                Shipping: new ExternalShippingInfo(
                    FreeShipping: null,
                    ShippingMode: null,
                    DeliveryPromise: null,
                    PickupAvailable: null
                ),
                Seller: new ExternalSellerInfo(
                    SellerId: "STORE-001",
                    Name: "Almacen Local",
                    OfficialStore: true,
                    SellerType: "store",
                    ReputationLevel: null,
                    Badges: "Proveedor Local"
                ),
                Media: new ExternalProductMedia(
                    ThumbnailUrl: "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=800&q=80",
                    ImageUrls: new[]
                    {
                        "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=1200&q=80"
                    },
                    VideoUrls: Array.Empty<string>()
                ),
                Attributes: new Dictionary<string, string>
                {
                    ["Marca"] = "Local",
                    ["Presentacion"] = "1 kg",
                    ["Tipo"] = "Largo fino"
                },
                Provider: "local",
                Source: "Pricer Seed",
                ScrapeProvider: null,
                HtmlVersion: "seed-v1",
                SelectorVersion: "seed-v1",
                FetchedAtUtc: fetchedAt,
                Warnings: new[] { "Datos simulados para desarrollo." },
                Location: "Cordoba"
            )
        };

        return items;
    }
}
