using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pricer.Domain.Entities;

namespace Pricer.Infrastructure.Persistence.Configurations;

public sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", "dbo");
        b.HasKey(x => x.UserId);

        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();

        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
