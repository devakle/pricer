namespace Pricer.Domain.Entities;

public sealed class PriceReport
{
    public Guid ReportId { get; set; }
    public Guid StoreId { get; set; }
    public Guid SkuId { get; set; }
    public Guid UserId { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = "ARS";
    public DateTime ReportedAt { get; set; }

    public string Source { get; set; } = "manual";
    public string? EvidenceUrl { get; set; }

    public byte Confidence { get; set; } = 50;
    public bool IsFlagged { get; set; }

    public Store Store { get; set; } = default!;
    public Sku Sku { get; set; } = default!;
    public User User { get; set; } = default!;
}
