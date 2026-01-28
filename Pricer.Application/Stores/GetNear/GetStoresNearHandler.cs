using Pricer.Application.Common;

namespace Pricer.Application.Stores.GetNear;

public sealed class GetStoresNearHandler
{
    private readonly IStoreReadRepository _repo;

    public GetStoresNearHandler(IStoreReadRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<StoreNearDto>>> Handle(GetStoresNearQuery q, CancellationToken ct)
    {
        if (q.RadiusKm <= 0 || q.RadiusKm > 50)
            return Result<IReadOnlyList<StoreNearDto>>.Fail("validation.radiusKm", "radiusKm inválido (1..50).");

        var take = q.Take is <= 0 or > 200 ? 50 : q.Take;

        var data = await _repo.GetNearAsync(q.Lat, q.Lng, q.RadiusKm, take, ct);
        return Result<IReadOnlyList<StoreNearDto>>.Ok(data);
    }
}
