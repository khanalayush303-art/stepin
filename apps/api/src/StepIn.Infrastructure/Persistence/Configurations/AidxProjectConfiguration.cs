using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxProjectConfiguration : IEntityTypeConfiguration<AidxProject>
{
    public void Configure(EntityTypeBuilder<AidxProject> builder)
    {
        builder.ToTable("AidxProjects", "aidx");

        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(220).IsRequired();
        builder.Property(p => p.ShortDescription).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(20000).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ExternalUrl).HasMaxLength(500);
        builder.Property(p => p.ImageKey).HasMaxLength(500);
        builder.Property(p => p.ImageAltText).HasMaxLength(300);

        // Previously unconfigured, so it silently fell back to the database-wide default string
        // length (512 — see ApplicationDbContext.ConfigureConventions). Freeform results/impact
        // text routinely runs longer than that, and nothing validated it before it reached
        // SaveChangesAsync, so a sufficiently long Outcomes value crashed project creation/editing
        // with an unhandled Postgres "value too long" error instead of a clean validation message.
        // Matches Description's cap, consistent with its own "freeform... text" doc comment.
        builder.Property(p => p.Outcomes).HasMaxLength(20000);

        // Only canonical rows (RevisionOfId IS NULL) compete for slug uniqueness — a published
        // row and its in-progress revision never collide, since the revision is excluded.
        builder.HasIndex(p => p.Slug).IsUnique().HasFilter("\"RevisionOfId\" IS NULL");
        builder.HasIndex(p => new { p.Status, p.PublishedAt });
        builder.HasIndex(p => p.CreatedByMembershipId);

        // At most one still-open (not yet merged/discarded) revision per canonical row — a real
        // database constraint, not just trust that the state machine always prevents a second one.
        builder.HasIndex(p => p.RevisionOfId)
            .IsUnique()
            .HasDatabaseName("IX_AidxProjects_OneOpenRevisionPerCanonical")
            .HasFilter("\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

        builder.HasOne<AidxMembership>()
            .WithMany()
            .HasForeignKey(p => p.CreatedByMembershipId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AidxProject>()
            .WithMany()
            .HasForeignKey(p => p.RevisionOfId)
            .OnDelete(DeleteBehavior.Cascade);

        // Postgres' built-in xmin system column as the optimistic-concurrency token — no new
        // storage. UseXminAsConcurrencyToken() is not available on the exact Npgsql provider
        // version resolved in this project (verified by compiling against it, not inferred from
        // the floating "10.0.*" package reference), so it's mapped manually here instead — same
        // effect. Defense-in-depth for the true check-to-save race window; the primary stale-edit
        // guard is the client-supplied expectedVersion compared explicitly before any field
        // mutation (AidxContentEditingService), not this alone.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasMany(p => p.ResearchAreas)
            .WithOne()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Technologies)
            .WithOne()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Researchers)
            .WithOne()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
