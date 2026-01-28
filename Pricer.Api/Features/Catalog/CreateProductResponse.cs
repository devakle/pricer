namespace Pricer.Api.Features.Catalog;

public sealed record CreateProductResponse(
    Guid ProductId,
    Guid SkuId,
    string? ImageUrl,
    Guid? PriceReportId
);
