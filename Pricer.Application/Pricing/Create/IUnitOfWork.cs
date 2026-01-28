namespace Pricer.Application.Pricing.Create;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
