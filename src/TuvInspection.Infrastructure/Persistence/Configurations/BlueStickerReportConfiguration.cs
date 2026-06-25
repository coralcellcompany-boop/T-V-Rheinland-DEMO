using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.BlueSticker;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class BlueStickerReportConfiguration : IEntityTypeConfiguration<BlueStickerReport>
{
    public void Configure(EntityTypeBuilder<BlueStickerReport> e)
    {
        e.ToTable("BlueStickerReports");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.ReportNo).IsRequired().HasMaxLength(50);
        e.HasIndex(x => x.ReportNo).IsUnique();
        e.Property(x => x.JobOrderId).IsRequired();
        e.Property(x => x.EquipmentId).IsRequired();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.TuvJobOrderNo).IsRequired().HasMaxLength(50);
        e.Property(x => x.EquipmentIdNo).IsRequired().HasMaxLength(100);

        foreach (var p in new[] { "AramcoCategoryNo", "OrgCode", "RpoNo", "CrmNo",
            "DepartmentContractor", "AreaOfInspection", "Capacity", "EquipmentLocation",
            "Manufacturer", "Model", "EquipmentType", "EquipmentSerialNo", "PreviousStickerNo",
            "PreviousStickerIssuedBy", "NewStickerNo", "ReceiverName", "ReceiverBadgeNo",
            "ReceiverTelephone", "InspectorName", "InspectorSapNo", "InspectorTelephone",
            "TechnicalReviewerName", "ClientOtpSentToEmail" })
            e.Property(p).HasMaxLength(300);

        e.Property(x => x.Deficiencies).HasColumnType("nvarchar(max)");
        e.Property(x => x.CorrectiveActionsTaken).HasColumnType("nvarchar(max)");
        e.Property(x => x.ReceiverSignaturePng).HasColumnType("nvarchar(max)");
        e.Property(x => x.InspectorSignaturePng).HasColumnType("nvarchar(max)");
        e.Property(x => x.TechnicalReviewerSignaturePng).HasColumnType("nvarchar(max)");
        e.Property(x => x.ClientOtpHash).HasMaxLength(200);

        e.Property(x => x.Result).HasConversion<int>();
        e.Property(x => x.State).HasConversion<int>();
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);

        e.HasIndex(x => x.State);
        e.HasIndex(x => x.JobOrderId);
        e.HasIndex(x => x.EquipmentId);
        e.HasIndex(x => x.StickerId).IsUnique().HasFilter("[StickerId] IS NOT NULL");

        e.HasMany(x => x.Transitions)
            .WithOne()
            .HasForeignKey(t => t.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Navigation(x => x.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);

        e.Ignore(x => x.DomainEvents);
    }
}

public class BlueStickerReportStateTransitionConfiguration : IEntityTypeConfiguration<BlueStickerReportStateTransition>
{
    public void Configure(EntityTypeBuilder<BlueStickerReportStateTransition> e)
    {
        e.ToTable("BlueStickerReportTransitions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.FromState).HasConversion<int>();
        e.Property(x => x.ToState).HasConversion<int>();
        e.Property(x => x.ActorUserId).IsRequired().HasMaxLength(450);
        e.Property(x => x.ActorRole).IsRequired().HasMaxLength(50);
        e.Property(x => x.Comments).HasMaxLength(2000);
        e.HasIndex(x => x.ReportId);
    }
}
