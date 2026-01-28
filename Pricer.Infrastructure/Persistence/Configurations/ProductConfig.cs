using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pricer.Domain.Entities;

namespace Pricer.Infrastructure.Persistence.Configurations;

public sealed class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", "dbo");
        b.HasKey(x => x.ProductId);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameNormalized).HasMaxLength(250).IsRequired();
        b.Property(x => x.Brand).HasMaxLength(120);
        b.Property(x => x.Category).HasMaxLength(120);
        b.Property(x => x.ImageUrl).HasMaxLength(400);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(x => x.NameNormalized).HasDatabaseName("IX_products_name_normalized");
    }
}
