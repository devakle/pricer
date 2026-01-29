using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pricer.Api.Common.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pricer.Api.Features.ExternalProducts;

[ApiController]
[Route("api/external-products")]
public sealed class ExternalProductsController : ControllerBase
{
    private readonly ScrapingBeeSearchClient _scrapingBeeSearchClient;

    public ExternalProductsController(ScrapingBeeSearchClient scrapingBeeSearchClient)
    {
        _scrapingBeeSearchClient = scrapingBeeSearchClient;
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
            && normalizedProvider is not ("all" or "mercadolibre" or "local"))
        {
            return BadRequest(ApiResponse<IReadOnlyList<ExternalProductDto>>.Failure(
                "provider_not_supported",
                "Proveedor no soportado."
            ));
        }

        var data = new List<ExternalProductDto>();

        try
        {
            if (normalizedProvider is null or "all" or "mercadolibre")
            {
                if (!string.IsNullOrWhiteSpace(rawQuery))
                {
                    var mercadoLibreItems = await _scrapingBeeSearchClient.SearchAsync(
                        rawQuery,
                        take,
                        cancellationToken
                    );
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

        return Ok(ApiResponse<IReadOnlyList<ExternalProductDto>>.Success(withPositions));
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
