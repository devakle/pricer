namespace Pricer.Api.Features.Pricing.Create;

public sealed record CreatePriceReportRequest(
    Guid StoreId,
    Guid SkuId,
    decimal Price,
    string Currency,
    string Source,
    string? EvidenceUrl
);
