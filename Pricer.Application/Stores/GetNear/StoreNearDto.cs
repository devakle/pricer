namespace Pricer.Application.Stores.GetNear;

public sealed record StoreNearDto(
    Guid StoreId,
    string Name,
    string? ChainName,
    string? Address,
    string? City,
    double Lat,
    double Lng,
    double DistanceMeters
);
