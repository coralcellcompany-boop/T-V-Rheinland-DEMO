using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.Equipment;
using EquipmentEntity = TuvInspection.Domain.Equipment.Equipment;
using EquipmentType = TuvInspection.Domain.Equipment.EquipmentType;
using AramcoCategory = TuvInspection.Domain.Equipment.AramcoCategory;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class EquipmentTypeConfiguration : IEntityTypeConfiguration<EquipmentType>
{
    public void Configure(EntityTypeBuilder<EquipmentType> e)
    {
        e.ToTable("EquipmentTypes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        e.HasIndex(x => x.Name).IsUnique();
        e.Property(x => x.AramcoCategory).HasConversion<int?>();
        e.Property(x => x.DefaultStandards).HasMaxLength(500);
        e.Property(x => x.MsReference).HasMaxLength(50);
        e.Property(x => x.Annex).HasMaxLength(50);
    }
}

public class EquipmentConfiguration : IEntityTypeConfiguration<EquipmentEntity>
{
    public void Configure(EntityTypeBuilder<EquipmentEntity> e)
    {
        e.ToTable("Equipment");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.EquipmentTypeId).IsRequired();
        e.Property(x => x.IdNo).IsRequired().HasMaxLength(100);
        e.Property(x => x.SerialNo).HasMaxLength(150);
        e.Property(x => x.Manufacturer).HasMaxLength(150);
        e.Property(x => x.Model).HasMaxLength(150);
        e.Property(x => x.Swl).HasMaxLength(50);
        e.Property(x => x.Location).HasMaxLength(300);
        e.Property(x => x.PhotoKey).HasMaxLength(500);
        e.Property(x => x.AramcoCategory).HasConversion<int?>();
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);

        e.HasIndex(x => new { x.ClientId, x.IdNo }).IsUnique();
        e.HasOne<TuvInspection.Domain.Clients.Client>()
            .WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<EquipmentType>()
            .WithMany().HasForeignKey(x => x.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);

        e.Ignore(x => x.DomainEvents);
    }
}

public class DefectCodeConfiguration : IEntityTypeConfiguration<DefectCode>
{
    public void Configure(EntityTypeBuilder<DefectCode> e)
    {
        e.ToTable("DefectCodes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Code).IsRequired().HasMaxLength(40);
        e.Property(x => x.Description).IsRequired().HasMaxLength(500);
        e.Property(x => x.Severity).IsRequired().HasMaxLength(20);
        e.HasIndex(x => new { x.EquipmentTypeId, x.Code }).IsUnique();
        e.HasOne<EquipmentType>().WithMany()
            .HasForeignKey(x => x.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
