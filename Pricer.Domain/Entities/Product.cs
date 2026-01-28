namespace Pricer.Domain.Entities;

public sealed class Product
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string NameNormalized { get; set; } = default!;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
