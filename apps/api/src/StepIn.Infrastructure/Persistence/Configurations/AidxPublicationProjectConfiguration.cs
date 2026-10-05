using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxPublicationProjectConfiguration : IEntityTypeConfiguration<AidxPublicationProject>
{
    public void Configure(EntityTypeBuilder<AidxPublicationProject> builder)
    {
        builder.ToTable("AidxPublicationProjects", "aidx");

        builder.HasKey(x => new { x.PublicationId, x.ProjectId });

        builder.HasOne<AidxPublication>()
            .WithMany(p => p.Projects)
            .HasForeignKey(x => x.PublicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProjectId);
    }
}
