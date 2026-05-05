using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.Outbox;
using TuvInspection.Infrastructure.Identity;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> e)
    {
        e.Property(x => x.FullName).HasMaxLength(200);
        e.Property(x => x.SapNo).HasMaxLength(50);
        e.Property(x => x.CertNo).HasMaxLength(50);
        e.Property(x => x.AssignedClientIdsCsv).HasMaxLength(4000).IsRequired();
        e.Property(x => x.LicenseNumber).HasMaxLength(80);
        e.Property(x => x.LicenseAuthority).HasMaxLength(150);
        e.Property(x => x.LicenseScope).HasMaxLength(300);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("RefreshTokens");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        e.Property(x => x.TokenHash).IsRequired().HasMaxLength(200);
        e.HasIndex(x => x.TokenHash).IsUnique();
        e.HasIndex(x => x.UserId);
        e.Property(x => x.ReplacedByTokenHash).HasMaxLength(200);
        e.Property(x => x.CreatedFromIp).HasMaxLength(45);
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> e)
    {
        e.ToTable("OutboxMessages");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Type).IsRequired().HasMaxLength(200);
        e.Property(x => x.PayloadJson).IsRequired().HasColumnType("nvarchar(max)");
        e.Property(x => x.LastError).HasMaxLength(2000);
        e.HasIndex(x => x.ProcessedAtUtc);
    }
}
