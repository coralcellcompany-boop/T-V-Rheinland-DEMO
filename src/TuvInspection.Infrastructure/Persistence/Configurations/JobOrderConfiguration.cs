using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        // A ValueComparer is REQUIRED: this is a mutable List<string> behind a value converter.
        // Without it EF snapshots the same list reference, so in-place AssignInspector/
        // UnassignInspector mutations are never detected and SaveChanges silently drops them.
        var jsonOptions = new JsonSerializerOptions();
        var inspectorListComparer = new ValueComparer<List<string>>(
            (a, b) => (a == null && b == null)
                || (a != null && b != null && a.SequenceEqual(b)),
            v => v == null
                ? 0
                : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s == null ? 0 : s.GetHashCode())),
            v => v == null ? new List<string>() : v.ToList());

        var assignedInspectorIds = e.Property<List<string>>("_assignedInspectorIds")
            .HasField("_assignedInspectorIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("AssignedInspectorIds")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());
        assignedInspectorIds.Metadata.SetValueComparer(inspectorListComparer);
        e.Ignore(x => x.AssignedInspectorIds);

        // Attachment storage keys (PDF/images) stored as JSON column — small list read with the job.
        // Same ValueComparer requirement as the inspector list above (mutable List<string>).
        var attachmentKeys = e.Property<List<string>>("_attachmentKeys")
            .HasField("_attachmentKeys")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("AttachmentKeys")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());
        attachmentKeys.Metadata.SetValueComparer(inspectorListComparer);
        e.Ignore(x => x.AttachmentKeys);
        e.Ignore(x => x.DomainEvents);
    }
}
