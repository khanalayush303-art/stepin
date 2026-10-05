using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxPublicationAuthorConfiguration : IEntityTypeConfiguration<AidxPublicationAuthor>
{
    public void Configure(EntityTypeBuilder<AidxPublicationAuthor> builder)
    {
        builder.ToTable("AidxPublicationAuthors", "aidx");

        builder.HasKey(x => new { x.PublicationId, x.Position });

        builder.Property(x => x.ExternalAuthorName).HasMaxLength(200);

        builder.HasOne<AidxPublication>()
            .WithMany(p => p.Authors)
            .HasForeignKey(x => x.PublicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a researcher with authorship history cannot be deleted silently.
        // Admins must reassign or remove the authorship first.
        builder.HasOne(x => x.Researcher)
            .WithMany()
            .HasForeignKey(x => x.ResearcherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ResearcherId);
    }
}
