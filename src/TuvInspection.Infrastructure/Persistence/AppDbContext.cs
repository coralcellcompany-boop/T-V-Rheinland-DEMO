using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Persistence;
using TuvInspection.Domain.Assessments;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.JobOrders;
using TuvInspection.Domain.Outbox;
using TuvInspection.Domain.Stickers;
using TuvInspection.Infrastructure.Identity;
using EquipmentEntity = TuvInspection.Domain.Equipment.Equipment;
using EquipmentType = TuvInspection.Domain.Equipment.EquipmentType;

namespace TuvInspection.Infrastructure.Persistence;

/// <summary>
/// Primary application DbContext — owns Identity tables, business aggregates, refresh tokens, outbox.
/// The audit log lives in <see cref="AuditDbContext"/> (separate user, INSERT-only) so that even a
/// compromise of the application DB credentials cannot rewrite history.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>, IUnitOfWork
{
    private readonly ITenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<EquipmentEntity> Equipment => Set<EquipmentEntity>();
    public DbSet<InspectionCertificate> Certificates => Set<InspectionCertificate>();
    public DbSet<CertificateStateTransition> CertificateTransitions => Set<CertificateStateTransition>();
    public DbSet<JobOrder> JobOrders => Set<JobOrder>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Sticker> Stickers => Set<Sticker>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentTransition> AssessmentTransitions => Set<AssessmentTransition>();
    public DbSet<CompetencyCard> CompetencyCards => Set<CompetencyCard>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Tenant scoping. Manager bypass is intentionally NOT inside the filter — it's an
        // explicit per-query .IgnoreQueryFilters() with a comment, so leaks are visible in code.
        builder.Entity<EquipmentEntity>().HasQueryFilter(e =>
            _tenant.IsAnonymous || _tenant.AssignedClientIds.Contains(e.ClientId));
        builder.Entity<InspectionCertificate>().HasQueryFilter(c =>
            _tenant.IsAnonymous || _tenant.AssignedClientIds.Contains(c.ClientId));
        builder.Entity<JobOrder>().HasQueryFilter(j =>
            _tenant.IsAnonymous || _tenant.AssignedClientIds.Contains(j.ClientId));
        builder.Entity<Candidate>().HasQueryFilter(c =>
            _tenant.IsAnonymous || _tenant.AssignedClientIds.Contains(c.ClientId));
        builder.Entity<Assessment>().HasQueryFilter(a =>
            _tenant.IsAnonymous || _tenant.AssignedClientIds.Contains(a.ClientId));
    }

    public Task<int> SaveChanges(CancellationToken ct) => SaveChangesAsync(ct);
}
