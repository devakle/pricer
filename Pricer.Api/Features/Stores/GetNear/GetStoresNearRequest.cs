namespace Pricer.Api.Features.Stores.GetNear;

public sealed record GetStoresNearRequest(double lat, double lng, double radiusKm = 2, int take = 50);
