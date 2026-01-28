namespace Pricer.Api.Features.Stores.Create;

public sealed record CreateStoreRequest(
    string Name,
    string? ChainName,
    string? Address,
    string? City,
    double Lat,
    double Lng
);
