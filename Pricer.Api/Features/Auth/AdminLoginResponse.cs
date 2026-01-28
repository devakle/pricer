namespace Pricer.Api.Features.Auth;

public sealed record AdminLoginResponse(string Token, DateTime ExpiresAt);
