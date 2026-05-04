using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuvInspection.Domain.Assessments;
using TuvInspection.Domain.Clients;

namespace TuvInspection.Infrastructure.Persistence.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> e)
    {
        e.ToTable("Candidates");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        e.Property(x => x.IdentificationNumber).IsRequired().HasMaxLength(50);
        e.Property(x => x.Phone).HasMaxLength(50);
        e.Property(x => x.Email).HasMaxLength(200);
        e.Property(x => x.EmployeeNo).HasMaxLength(50);
        e.Property(x => x.Nationality).HasMaxLength(80);
        e.Property(x => x.PhotoKey).HasMaxLength(500);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.HasIndex(x => new { x.ClientId, x.IdentificationNumber }).IsUnique();
        e.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        e.Ignore(x => x.DomainEvents);
    }
}

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> e)
    {
        e.ToTable("Assessments");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.AssessmentNo).IsRequired().HasMaxLength(40);
        e.HasIndex(x => x.AssessmentNo).IsUnique();
        e.Property(x => x.CandidateId).IsRequired();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.Category).HasConversion<int>();
        e.Property(x => x.Result).HasConversion<int>();
        e.Property(x => x.State).HasConversion<int>();
        e.Property(x => x.Location).HasMaxLength(300);
        e.Property(x => x.Comments).HasMaxLength(2000);
        e.Property(x => x.IssuedCardNo).HasMaxLength(40);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);

        e.HasMany(x => x.Transitions)
            .WithOne()
            .HasForeignKey(t => t.AssessmentId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Navigation(x => x.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);
        e.Ignore(x => x.DomainEvents);
    }
}

public class AssessmentTransitionConfiguration : IEntityTypeConfiguration<AssessmentTransition>
{
    public void Configure(EntityTypeBuilder<AssessmentTransition> e)
    {
        e.ToTable("AssessmentTransitions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.FromState).HasConversion<int>();
        e.Property(x => x.ToState).HasConversion<int>();
        e.Property(x => x.ActorUserId).IsRequired().HasMaxLength(450);
        e.Property(x => x.ActorRole).IsRequired().HasMaxLength(50);
        e.Property(x => x.Comments).HasMaxLength(2000);
        e.HasIndex(x => x.AssessmentId);
    }
}

public class CompetencyCardConfiguration : IEntityTypeConfiguration<CompetencyCard>
{
    public void Configure(EntityTypeBuilder<CompetencyCard> e)
    {
        e.ToTable("CompetencyCards");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.CardNo).IsRequired().HasMaxLength(40);
        e.HasIndex(x => x.CardNo).IsUnique();
        e.Property(x => x.AssessmentId).IsRequired();
        e.HasIndex(x => x.AssessmentId).IsUnique();
        e.Property(x => x.CandidateId).IsRequired();
        e.Property(x => x.ClientId).IsRequired();
        e.Property(x => x.Category).HasConversion<int>();
        e.Property(x => x.State).HasConversion<int>();
        e.Property(x => x.StatusReason).HasMaxLength(500);
        e.Property(x => x.CreatedById).HasMaxLength(450);
        e.Property(x => x.UpdatedById).HasMaxLength(450);
        e.Ignore(x => x.DomainEvents);
    }
}
