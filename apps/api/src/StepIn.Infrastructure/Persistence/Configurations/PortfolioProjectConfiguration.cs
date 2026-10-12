using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Portfolio;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class PortfolioProjectConfiguration : IEntityTypeConfiguration<PortfolioProject>
{
    public void Configure(EntityTypeBuilder<PortfolioProject> builder)
    {
        builder.Property(p => p.Title).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(5000);
        builder.Property(p => p.RepositoryUrl).HasMaxLength(500);
        builder.Property(p => p.DemoUrl).HasMaxLength(500);
        builder.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(10);
        builder.Property(p => p.Skills).HasColumnType("text[]");

        builder.HasIndex(p => p.CandidateProfileId);
        builder.HasIndex(p => p.ResearchProjectId);
        // Supports the recruiter-visible lookup: this candidate's Public projects only.
        builder.HasIndex(p => new { p.CandidateProfileId, p.Visibility });

        // Optional link to a published AIDX project. SetNull so nothing about that project's own
        // lifecycle can ever remove a candidate's own portfolio entry.
        builder.HasOne(p => p.ResearchProject)
            .WithMany()
            .HasForeignKey(p => p.ResearchProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
