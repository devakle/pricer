using Pricer.Application.Common;
using Pricer.Domain.Entities;

namespace Pricer.Application.Pricing.Create;

public sealed class CreatePriceReportHandler
{
    private readonly IStoreExistsChecker _storeExists;
    private readonly ISkuExistsChecker _skuExists;
    private readonly IPriceReportRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreatePriceReportHandler(
        IStoreExistsChecker storeExists,
        ISkuExistsChecker skuExists,
        IPriceReportRepository repo,
        IUnitOfWork uow)
        => (_storeExists, _skuExists, _repo, _uow) = (storeExists, skuExists, repo, uow);

    public async Task<Result<CreatePriceReportResponse>> Handle(CreatePriceReportCommand c, CancellationToken ct)
    {
        if (c.Price <= 0)
            return Result<CreatePriceReportResponse>.Fail("validation.price", "El precio debe ser > 0.");

        if (string.IsNullOrWhiteSpace(c.Currency) || c.Currency.Trim().Length != 3)
            return Result<CreatePriceReportResponse>.Fail("validation.currency", "Currency debe ser ISO3 (ej: ARS).");

        if (!await _storeExists.ExistsAsync(c.StoreId, ct))
            return Result<CreatePriceReportResponse>.Fail("not_found.store", "El comercio no existe.");

        if (!await _skuExists.ExistsAsync(c.SkuId, ct))
            return Result<CreatePriceReportResponse>.Fail("not_found.sku", "El producto (SKU) no existe.");

        var report = new PriceReport
        {
            ReportId = Guid.NewGuid(),
            UserId = c.UserId,
            StoreId = c.StoreId,
            SkuId = c.SkuId,
            Price = c.Price,
            Currency = c.Currency.Trim().ToUpperInvariant(),
            Source = string.IsNullOrWhiteSpace(c.Source) ? "manual" : c.Source.Trim(),
            EvidenceUrl = string.IsNullOrWhiteSpace(c.EvidenceUrl) ? null : c.EvidenceUrl.Trim(),
            ReportedAt = DateTime.UtcNow,
            Confidence = 50,
            IsFlagged = false
        };

        await _repo.AddAsync(report, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<CreatePriceReportResponse>.Ok(new(report.ReportId, report.ReportedAt));
    }
}
