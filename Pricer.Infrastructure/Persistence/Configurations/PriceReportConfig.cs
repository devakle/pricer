using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pricer.Domain.Entities;

namespace Pricer.Infrastructure.Persistence.Configurations;

public sealed class PriceReportConfig : IEntityTypeConfiguration<PriceReport>
{
    public void Configure(EntityTypeBuilder<PriceReport> b)
    {
        b.ToTable("price_reports");
        b.HasKey(x => x.ReportId);

        b.Property(x => x.Price).HasPrecision(18, 2);

        b.HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId);

        b.HasOne(x => x.Sku)
            .WithMany()
            .HasForeignKey(x => x.SkuId);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);
    }
}
