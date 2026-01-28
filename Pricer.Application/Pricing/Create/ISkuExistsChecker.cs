namespace Pricer.Application.Pricing.Create;

public interface ISkuExistsChecker
{
    Task<bool> ExistsAsync(Guid skuId, CancellationToken ct);
}
