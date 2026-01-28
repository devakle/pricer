namespace Pricer.Application.Pricing.Create;

public interface IStoreExistsChecker
{
    Task<bool> ExistsAsync(Guid storeId, CancellationToken ct);
}
