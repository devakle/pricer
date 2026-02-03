using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pricer.Domain.Entities;

namespace Pricer.Infrastructure.Persistence.Configurations;

public sealed class SkuConfig : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> b)
    {
        b.ToTable("skus");
        b.HasKey(x => x.SkuId);

        b.Property(x => x.DisplayName).HasMaxLength(250).IsRequired();
        b.Property(x => x.DisplayNameNormalized).HasMaxLength(300).IsRequired();
        b.Property(x => x.SizeUnit).HasMaxLength(20);
        b.Property(x => x.SizeValue).HasPrecision(18, 2);
        b.Property(x => x.Barcode).HasMaxLength(64);

        b.HasIndex(x => x.Barcode).IsUnique().HasFilter("[barcode] IS NOT NULL");
        b.HasIndex(x => x.DisplayNameNormalized);

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
