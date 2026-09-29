using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Profiles;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class CandidateEducationConfiguration : IEntityTypeConfiguration<CandidateEducation>
{
    public void Configure(EntityTypeBuilder<CandidateEducation> builder)
    {
        builder.ToTable("CandidateEducations");

        builder.Property(e => e.Institution).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Degree).HasMaxLength(200);
        builder.Property(e => e.FieldOfStudy).HasMaxLength(200);
        builder.Property(e => e.StartDate).HasColumnType("date");
        builder.Property(e => e.EndDate).HasColumnType("date");
        builder.Property(e => e.Description).HasMaxLength(1000);
    }
}
