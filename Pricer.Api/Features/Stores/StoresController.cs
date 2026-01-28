using Microsoft.AspNetCore.Mvc;
using Pricer.Api.Common.Api;
using Pricer.Api.Features.Stores.GetNear;
using Pricer.Application.Stores.GetNear;

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
}
