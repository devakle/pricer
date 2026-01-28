using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Pricer.Api.Common.Api;
using Pricer.Api.Common.Auth;
using Pricer.Api.Features.Pricing.Create;
using Pricer.Application.Pricing.Create;
using Pricer.Infrastructure.Persistence;

namespace Pricer.Api.Features.Pricing;

[ApiController]
[Route("api/price-reports")]
public sealed class PriceReportsController : ControllerBase
{
    [Authorize(Policy = "CanReportPrice")]
    [HttpGet("near")]
    public async Task<IActionResult> Near(
        [FromServices] AppDbContext db,
        [FromQuery] Guid skuId,
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double radiusKm = 2,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (radiusKm <= 0 || radiusKm > 50)
            return BadRequest(ApiResponse<IReadOnlyList<PriceReportNearDto>>.Failure("validation.radiusKm", "radiusKm invalido (1..50)."));

        if (take <= 0 || take > 200) take = 50;

        var point = new Point(lng, lat) { SRID = 4326 };
        var radiusMeters = radiusKm * 1000;

        var query = db.PriceReports
            .AsNoTracking()
            .Where(x => x.SkuId == skuId)
            .Where(x => x.Store.Geo.Distance(point) <= radiusMeters)
            .GroupBy(x => x.StoreId)
            .Select(g => g.OrderByDescending(x => x.ReportedAt).First())
            .OrderByDescending(x => x.ReportedAt)
            .Take(take)
            .Select(x => new PriceReportNearDto(
                x.StoreId,
                x.Store.Name,
                x.Store.Geo.Y,
                x.Store.Geo.X,
                x.Price,
                x.Currency,
                x.ReportedAt));

        var data = await query.ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<PriceReportNearDto>>.Success(data));
    }

    [Authorize(Policy = "UserOnly")]
    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromServices] AppDbContext db,
        [FromQuery] Guid storeId,
        [FromQuery] Guid skuId,
        [FromQuery] int take = 30,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 200) take = 30;

        var data = await db.PriceReports
            .AsNoTracking()
            .Where(x => x.StoreId == storeId && x.SkuId == skuId)
            .OrderByDescending(x => x.ReportedAt)
            .Take(take)
            .Select(x => new PriceReportNearDto(
                x.StoreId,
                x.Store.Name,
                x.Store.Geo.Y,
                x.Store.Geo.X,
                x.Price,
                x.Currency,
                x.ReportedAt))
            .ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<PriceReportNearDto>>.Success(data));
    }

    [Authorize(Policy = "CanReportPrice")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreatePriceReportHandler handler,
        [FromBody] CreatePriceReportRequest req,
        CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();

        var result = await handler.Handle(new CreatePriceReportCommand(
            UserId: userId,
            StoreId: req.StoreId,
            SkuId: req.SkuId,
            Price: req.Price,
            Currency: req.Currency,
            Source: req.Source,
            EvidenceUrl: req.EvidenceUrl
        ), ct);

        return this.ToActionResult(result);
    }
}
