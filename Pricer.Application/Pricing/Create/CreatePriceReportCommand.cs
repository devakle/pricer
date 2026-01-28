namespace Pricer.Application.Pricing.Create;

public sealed record CreatePriceReportCommand(
    Guid UserId,
    Guid StoreId,
    Guid SkuId,
    decimal Price,
    string Currency,
    string Source,
    string? EvidenceUrl
);
