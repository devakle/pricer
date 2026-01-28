namespace Pricer.Domain.Entities;

public sealed class Sku
{
    public Guid SkuId { get; set; }
    public Guid ProductId { get; set; }

    public decimal? SizeValue { get; set; }
    public string? SizeUnit { get; set; }
    public int? PackCount { get; set; }

    public string? Barcode { get; set; }

    public string DisplayName { get; set; } = default!;
    public string DisplayNameNormalized { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = default!;
}
