using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pricer.Api.Common.Api;
using Pricer.Api.Common.Auth;
using Pricer.Api.Features.Pricing.Create;
using Pricer.Application.Pricing.Create;

namespace Pricer.Api.Features.Pricing;

[ApiController]
[Route("api/price-reports")]
public sealed class PriceReportsController : ControllerBase
{
    [Authorize]
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
