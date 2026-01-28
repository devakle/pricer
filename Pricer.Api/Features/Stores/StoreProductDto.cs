namespace Pricer.Api.Features.Stores;

public sealed record StoreProductDto(
    Guid SkuId,
    string SkuDisplayName,
    string ProductName,
    string? Brand,
    string? ImageUrl,
    decimal Price,
    string Currency,
    DateTime ReportedAt
);
