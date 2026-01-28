using Pricer.Domain.Entities;

namespace Pricer.Application.Pricing.Create;

public interface IPriceReportRepository
{
    Task AddAsync(PriceReport report, CancellationToken ct);
}
