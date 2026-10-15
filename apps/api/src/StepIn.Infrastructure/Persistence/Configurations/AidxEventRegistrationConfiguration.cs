using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxEventRegistrationConfiguration : IEntityTypeConfiguration<AidxEventRegistration>
{
    public void Configure(EntityTypeBuilder<AidxEventRegistration> builder)
    {
        builder.ToTable("AidxEventRegistrations", "aidx");

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Email).HasMaxLength(320).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.CancellationTokenHash).HasMaxLength(100);

        // A signed-in user can only have one registration per event...
        builder.HasIndex(r => new { r.EventId, r.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        // ...and so can one normalized anonymous email, per event.
        builder.HasIndex(r => new { r.EventId, r.Email }).IsUnique().HasFilter("\"UserId\" IS NULL");

        builder.HasOne<AidxEvent>()
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
