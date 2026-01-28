namespace Pricer.Application.Stores.GetNear;

public sealed record GetStoresNearQuery(double Lat, double Lng, double RadiusKm, int Take);
