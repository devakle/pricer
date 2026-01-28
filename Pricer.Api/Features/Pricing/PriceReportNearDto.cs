namespace Pricer.Api.Features.Pricing;

public sealed record PriceReportNearDto(
    Guid StoreId,
    string StoreName,
    double Lat,
    double Lng,
    decimal Price,
    string Currency,
    DateTime ReportedAt
);
