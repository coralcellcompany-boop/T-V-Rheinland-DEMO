using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.Clients;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> e)
    {
        e.ToTable("Clients");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        e.Property(x => x.Code).IsRequired().HasMaxLength(40);
        e.HasIndex(x => x.Code).IsUnique();
        e.Property(x => x.Address).HasMaxLength(500);
        e.Property(x => x.ContactName).HasMaxLength(200);
        e.Property(x => x.ContactPhone).HasMaxLength(50);
        e.Property(x => x.ContactEmail).HasMaxLength(200);
        e.Property(x => x.ContractStatus).HasConversion<int>();
        e.Property(x => x.AllowedServices).HasConversion<int>();
        e.Property(x => x.CreatedAtUtc);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedAtUtc);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.Ignore(x => x.DomainEvents);
    }
}
