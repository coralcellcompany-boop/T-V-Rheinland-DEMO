using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.JobRequests;
using TuvInspection.Domain.Surveys;
using TuvInspection.Domain.Timesheets;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class JobRequestConfiguration : IEntityTypeConfiguration<JobRequest>
{
    public void Configure(EntityTypeBuilder<JobRequest> e)
    {
        e.ToTable("JobRequests");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.RequestNo).IsRequired().HasMaxLength(40);
        e.HasIndex(x => x.RequestNo).IsUnique();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.Service).HasConversion<int>();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.Site).HasMaxLength(300);
        e.Property(x => x.ContactName).HasMaxLength(200);
        e.Property(x => x.ContactPhone).HasMaxLength(50);
        e.Property(x => x.ContactEmail).HasMaxLength(200);
        e.Property(x => x.ScopeNotes).HasMaxLength(2000);
        e.Property(x => x.PoReference).HasMaxLength(80);
        e.Property(x => x.RejectionReason).HasMaxLength(500);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        e.Ignore(x => x.DomainEvents);
    }
}

public class DailyWorkReportConfiguration : IEntityTypeConfiguration<DailyWorkReport>
{
    public void Configure(EntityTypeBuilder<DailyWorkReport> e)
    {
        e.ToTable("DailyWorkReports");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.DwrNo).IsRequired().HasMaxLength(40);
        e.HasIndex(x => x.DwrNo).IsUnique();
        e.Property(x => x.JobOrderId).IsRequired();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.InspectorId).IsRequired().HasMaxLength(450);
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.Location).HasMaxLength(300);
        e.Property(x => x.Notes).HasMaxLength(2000);
        e.Property(x => x.RejectionReason).HasMaxLength(500);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.HasIndex(x => new { x.JobOrderId, x.InspectorId, x.Date });
        e.Ignore(x => x.HoursWorked);
        e.Ignore(x => x.DomainEvents);
    }
}

public class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> e)
    {
        e.ToTable("Surveys");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.SurveyNo).IsRequired().HasMaxLength(40);
        e.HasIndex(x => x.SurveyNo).IsUnique();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.Site).HasMaxLength(300);
        e.Property(x => x.GpsLatLng).HasMaxLength(80);
        e.Property(x => x.AccessNotes).HasMaxLength(2000);
        e.Property(x => x.SafetyNotes).HasMaxLength(2000);
        e.Property(x => x.Recommendation).HasMaxLength(2000);
        e.Property(x => x.SurveyorUserId).HasMaxLength(450);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        e.Ignore(x => x.DomainEvents);
    }
}
