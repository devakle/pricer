using Microsoft.EntityFrameworkCore;
using Pricer.Application.Pricing.Create;
using Pricer.Infrastructure.Persistence;

namespace Pricer.Infrastructure.Repositories.Stores;

public sealed class StoreExistsChecker : IStoreExistsChecker
{
    private readonly AppDbContext _db;
    public StoreExistsChecker(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid storeId, CancellationToken ct)
        => _db.Stores.AnyAsync(x => x.StoreId == storeId, ct);
}
