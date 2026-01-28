using Microsoft.EntityFrameworkCore;
using Pricer.Application.Pricing.Create;
using Pricer.Infrastructure.Persistence;

namespace Pricer.Infrastructure.Repositories.Catalog;

public sealed class SkuExistsChecker : ISkuExistsChecker
{
    private readonly AppDbContext _db;
    public SkuExistsChecker(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid skuId, CancellationToken ct)
        => _db.Skus.AnyAsync(x => x.SkuId == skuId, ct);
}
