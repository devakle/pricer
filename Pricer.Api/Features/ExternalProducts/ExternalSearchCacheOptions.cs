namespace Pricer.Api.Features.ExternalProducts;

public sealed class ExternalSearchCacheOptions
{
    public bool Enabled { get; init; } = true;
    public int TtlSeconds { get; init; } = 300;
}
