using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.Certificates;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class InspectionCertificateConfiguration : IEntityTypeConfiguration<InspectionCertificate>
{
    public void Configure(EntityTypeBuilder<InspectionCertificate> e)
    {
        e.ToTable("InspectionCertificates");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.CertificateNo).IsRequired().HasMaxLength(50);
        e.HasIndex(x => x.CertificateNo).IsUnique();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.EquipmentId).IsRequired();
        e.Property(x => x.JobOrderId);
        e.Property(x => x.Standards).HasMaxLength(500);
        e.Property(x => x.StickerNo).HasMaxLength(50);
        e.Property(x => x.StickerId);
        e.HasIndex(x => x.StickerId).IsUnique().HasFilter("[StickerId] IS NOT NULL");
        e.Property(x => x.InspectionType).HasConversion<int>();
        e.Property(x => x.LoadTest).HasConversion<int>();
        e.Property(x => x.Result).HasConversion<int>();
        e.Property(x => x.State).HasConversion<int>();
        e.Property(x => x.ChecklistJson).HasColumnType("nvarchar(max)");
        e.Property(x => x.FindingsJson).HasColumnType("nvarchar(max)");
        e.Property(x => x.PhotosJson).HasColumnType("nvarchar(max)");
        e.Property(x => x.SignaturesJson).HasColumnType("nvarchar(max)");
        e.Property(x => x.AramcoReportJson).HasColumnType("nvarchar(max)");
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);

        e.HasMany(x => x.Transitions)
            .WithOne()
            .HasForeignKey(t => t.CertificateId)
            .OnDelete(DeleteBehavior.Cascade);

        e.Navigation(x => x.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);
        e.Ignore(x => x.DomainEvents);
    }
}

public class CertificateStateTransitionConfiguration : IEntityTypeConfiguration<CertificateStateTransition>
{
    public void Configure(EntityTypeBuilder<CertificateStateTransition> e)
    {
        e.ToTable("CertificateStateTransitions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.FromState).HasConversion<int>();
        e.Property(x => x.ToState).HasConversion<int>();
        e.Property(x => x.ActorUserId).IsRequired().HasMaxLength(450);
        e.Property(x => x.ActorRole).IsRequired().HasMaxLength(50);
        e.Property(x => x.Comments).HasMaxLength(2000);
        e.HasIndex(x => x.CertificateId);
    }
}
