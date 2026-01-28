using NetTopologySuite.Geometries;

namespace Pricer.Domain.Entities;

public sealed class Store
{
    public Guid StoreId { get; set; }
    public string Name { get; set; } = default!;
    public string? ChainName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Point Geo { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
