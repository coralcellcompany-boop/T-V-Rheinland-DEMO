using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using TuvInspection.Domain.JobOrders;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class JobOrderConfiguration : IEntityTypeConfiguration<JobOrder>
{
    public void Configure(EntityTypeBuilder<JobOrder> e)
    {
        e.ToTable("JobOrders");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.JobOrderNo).IsRequired().HasMaxLength(40);
        e.HasIndex(x => x.JobOrderNo).IsUnique();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.Service).HasConversion<int>();
        e.Property(x => x.Location).HasMaxLength(300);
        e.Property(x => x.Status).HasConversion<int>();

        // Inspector assignments stored as JSON column — small list, read together with the job.
        var jsonOptions = new JsonSerializerOptions();
        e.Property<List<string>>("_assignedInspectorIds")
            .HasField("_assignedInspectorIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("AssignedInspectorIds")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());
        e.Ignore(x => x.AssignedInspectorIds);
        e.Ignore(x => x.DomainEvents);
    }
}
