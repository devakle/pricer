namespace Pricer.Application.Stores.GetNear;

public interface IStoreReadRepository
{
    Task<IReadOnlyList<StoreNearDto>> GetNearAsync(
        double lat,
        double lng,
        double radiusKm,
        int take,
        CancellationToken ct);
}
