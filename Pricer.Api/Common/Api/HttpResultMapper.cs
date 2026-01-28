using Microsoft.AspNetCore.Mvc;
using Pricer.Api.Common.Api;
using Pricer.Application.Common;

namespace Pricer.Api.Common.Api;

public static class HttpResultMapper
{
    public static IActionResult ToActionResult<T>(this ControllerBase c, Result<T> r)
    {
        if (r.IsSuccess)
            return c.Ok(ApiResponse<T>.Success(r.Value!));

        var e = r.Error!;
        return e.Code switch
        {
            var x when x.StartsWith("validation.") => c.BadRequest(ApiResponse<T>.Failure(e.Code, e.Message)),
            var x when x.StartsWith("not_found.")  => c.NotFound(ApiResponse<T>.Failure(e.Code, e.Message)),
            var x when x.StartsWith("conflict.")   => c.Conflict(ApiResponse<T>.Failure(e.Code, e.Message)),
            var x when x.StartsWith("unauthorized.") => c.Unauthorized(ApiResponse<T>.Failure(e.Code, e.Message)),
            _ => c.StatusCode(500, ApiResponse<T>.Failure("unexpected", "Error inesperado."))
        };
    }
}
