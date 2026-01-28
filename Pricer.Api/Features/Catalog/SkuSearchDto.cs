namespace Pricer.Api.Features.Catalog;

public sealed record SkuSearchDto(
    Guid SkuId,
    string DisplayName,
    string ProductName,
    string? Brand,
    decimal? SizeValue,
    string? SizeUnit
);
