using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Pricer.Application.Stores.GetNear;
using Pricer.Infrastructure.Persistence;

namespace Pricer.Infrastructure.Repositories.Stores;

public sealed class StoreReadRepository : IStoreReadRepository
{
    private readonly AppDbContext _db;
    private readonly GeometryFactory _gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public StoreReadRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<StoreNearDto>> GetNearAsync(double lat, double lng, double radiusKm, int take, CancellationToken ct)
    {
        var userPoint = _gf.CreatePoint(new Coordinate(lng, lat)); // x=lng y=lat

        return await _db.Stores
            .Where(s => s.Geo.Distance(userPoint) <= radiusKm * 1000)
            .OrderBy(s => s.Geo.Distance(userPoint))
            .Take(take)
            .Select(s => new StoreNearDto(
                s.StoreId,
                s.Name,
                s.ChainName,
                s.Address,
                s.City,
                s.Geo.Y,
                s.Geo.X,
                s.Geo.Distance(userPoint)
            ))
            .ToListAsync(ct);
    }
}
