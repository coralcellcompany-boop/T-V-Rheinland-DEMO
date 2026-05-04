using Microsoft.EntityFrameworkCore;
using TuvInspection.Domain.Auditing;

namespace TuvInspection.Infrastructure.Persistence;

/// <summary>
/// Holds only the AuditLog table. Configured in DI to use a connection string with a dedicated
/// SQL user that has only INSERT permission — not UPDATE, not DELETE. Without role separation
/// at the DB level, the "immutable audit" claim is one a real auditor will reject.
/// </summary>
public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.EntityName).IsRequired().HasMaxLength(200);
            e.Property(x => x.EntityId).IsRequired().HasMaxLength(100);
            e.Property(x => x.Action).IsRequired().HasMaxLength(50);
            e.Property(x => x.ActorUserId).HasMaxLength(450);
            e.Property(x => x.ActorRole).HasMaxLength(50);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.PreviousHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.CurrentHash).IsRequired().HasMaxLength(128);
            e.HasIndex(x => x.AtUtc);
            e.HasIndex(x => new { x.EntityName, x.EntityId });
        });
    }
}
