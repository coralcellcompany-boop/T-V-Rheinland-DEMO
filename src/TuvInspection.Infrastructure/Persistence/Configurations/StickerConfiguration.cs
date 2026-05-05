using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.Stickers;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class StickerConfiguration : IEntityTypeConfiguration<Sticker>
{
    public void Configure(EntityTypeBuilder<Sticker> e)
    {
        e.ToTable("Stickers");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.StickerNo).IsRequired().HasMaxLength(20);
        e.HasIndex(x => x.StickerNo).IsUnique();
        e.Property(x => x.State).HasConversion<int>();
        e.Property(x => x.Color).HasConversion<int>();
        e.Property(x => x.VoidReason).HasMaxLength(500);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.Property(x => x.AssignedToInspectorId).HasMaxLength(450);
        e.HasIndex(x => x.State);
        e.HasIndex(x => x.Color);
        e.HasIndex(x => x.AssignedToInspectorId);
        e.HasIndex(x => x.IssuedToCertificateId);
        e.HasIndex(x => x.IssuedToEquipmentId);
        e.Ignore(x => x.DomainEvents);
    }
}

public class StickerRequestConfiguration : IEntityTypeConfiguration<StickerRequest>
{
    public void Configure(EntityTypeBuilder<StickerRequest> e)
    {
        e.ToTable("StickerRequests");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.RequestNo).IsRequired().HasMaxLength(30);
        e.HasIndex(x => x.RequestNo).IsUnique();
        e.Property(x => x.InspectorUserId).IsRequired().HasMaxLength(450);
        e.Property(x => x.Color).HasConversion<int>();
        e.Property(x => x.State).HasConversion<int>();
        e.Property(x => x.Justification).HasMaxLength(1000);
        e.Property(x => x.DecidedByUserId).HasMaxLength(450);
        e.Property(x => x.DecisionComments).HasMaxLength(1000);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.HasIndex(x => x.InspectorUserId);
        e.HasIndex(x => x.State);
        e.Ignore(x => x.DomainEvents);
    }
}
