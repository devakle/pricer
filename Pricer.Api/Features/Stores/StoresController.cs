using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Pricer.Api.Common.Api;
using Pricer.Api.Features.Stores.Create;
using Pricer.Api.Features.Stores.GetNear;
using Pricer.Application.Stores.GetNear;
using Pricer.Domain.Entities;
using Pricer.Infrastructure.Persistence;

namespace Pricer.Api.Features.Stores;

[ApiController]
[Route("api/stores")]
public sealed class StoresController : ControllerBase
{
    [HttpGet("near")]
    public async Task<IActionResult> Near(
        [FromServices] GetStoresNearHandler handler,
        [FromQuery] GetStoresNearRequest req,
        CancellationToken ct)
    {
        var result = await handler.Handle(
            new GetStoresNearQuery(req.lat, req.lng, req.radiusKm, req.take),
            ct);

        return this.ToActionResult(result);
    }

    [Authorize(Policy = "MerchantOnly")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] AppDbContext db,
        [FromBody] CreateStoreRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(ApiResponse<Guid>.Failure("validation.name", "Nombre requerido."));

        var store = new Store
        {
            StoreId = Guid.NewGuid(),
            Name = req.Name.Trim(),
            ChainName = string.IsNullOrWhiteSpace(req.ChainName) ? null : req.ChainName.Trim(),
            Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim(),
            City = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim(),
            Geo = new Point(req.Lng, req.Lat) { SRID = 4326 },
            CreatedAt = DateTime.UtcNow
        };

        await db.Stores.AddAsync(store, ct);
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<Guid>.Success(store.StoreId));
    }

    [Authorize(Policy = "UserOnly")]
    [HttpGet("{storeId:guid}/products")]
    public async Task<IActionResult> Products(
        [FromServices] AppDbContext db,
        [FromRoute] Guid storeId,
        [FromQuery] int take = 50,
        [FromQuery] int skip = 0,
        [FromQuery] string? currency = null,
        [FromQuery] string? brand = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 200) take = 50;
        if (skip < 0) skip = 0;
        var currencyFilter = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
        var brandFilter = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim().ToLowerInvariant();
        var categoryFilter = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant();

        var baseQuery = db.PriceReports
            .AsNoTracking()
            .Where(x => x.StoreId == storeId)
            .Where(x => currencyFilter == null || x.Currency == currencyFilter)
            .Where(x => brandFilter == null || (x.Sku.Product.Brand != null && x.Sku.Product.Brand.ToLower() == brandFilter))
            .Where(x => categoryFilter == null || (x.Sku.Product.Category != null && x.Sku.Product.Category.ToLower() == categoryFilter))
            .AsQueryable();

        var latestPerSku = baseQuery
            .GroupBy(x => x.SkuId)
            .Select(g => new { SkuId = g.Key, ReportedAt = g.Max(x => x.ReportedAt) });

        var query =
            from report in baseQuery
            join latest in latestPerSku
                on new { report.SkuId, report.ReportedAt }
                equals new { latest.SkuId, latest.ReportedAt }
            orderby report.ReportedAt descending
            select new StoreProductDto(
                report.SkuId,
                report.Sku.DisplayName,
                report.Sku.Product.Name,
                report.Sku.Product.Brand,
                report.Sku.Product.ImageUrl,
                report.Price,
                report.Currency,
                report.ReportedAt);

        query = query.Skip(skip).Take(take);

        var products = await query.ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<StoreProductDto>>.Success(products));
    }
}
