using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Profiles;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class CandidateCertificationConfiguration : IEntityTypeConfiguration<CandidateCertification>
{
    public void Configure(EntityTypeBuilder<CandidateCertification> builder)
    {
        builder.ToTable("CandidateCertifications");

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.IssuingOrganization).HasMaxLength(200);
        builder.Property(c => c.IssueDate).HasColumnType("date");
        builder.Property(c => c.CredentialUrl).HasMaxLength(300);
    }
}
