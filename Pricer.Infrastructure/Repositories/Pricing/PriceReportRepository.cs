using Pricer.Application.Pricing.Create;
using Pricer.Domain.Entities;
using Pricer.Infrastructure.Persistence;

namespace Pricer.Infrastructure.Repositories.Pricing;

public sealed class PriceReportRepository : IPriceReportRepository
{
    private readonly AppDbContext _db;
    public PriceReportRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(PriceReport report, CancellationToken ct)
        => await _db.PriceReports.AddAsync(report, ct);
}
