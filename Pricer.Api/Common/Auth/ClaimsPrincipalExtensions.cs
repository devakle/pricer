using System.Security.Claims;

namespace Pricer.Api.Common.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserIdOrThrow(this ClaimsPrincipal user)
    {
        // asumiendo claim "sub" o NameIdentifier
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var id))
            throw new InvalidOperationException("UserId claim inválido.");
        return id;
    }
}
