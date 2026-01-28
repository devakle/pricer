using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pricer.Domain.Entities;

namespace Pricer.Infrastructure.Persistence.Configurations;

public sealed class StoreConfig : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.ToTable("stores", "dbo");
        b.HasKey(x => x.StoreId);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ChainName).HasMaxLength(120);
        b.Property(x => x.Address).HasMaxLength(250);
        b.Property(x => x.City).HasMaxLength(120);

        b.Property(x => x.Geo)
            .HasColumnType("geography")
            .IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(x => x.City);
    }
}
