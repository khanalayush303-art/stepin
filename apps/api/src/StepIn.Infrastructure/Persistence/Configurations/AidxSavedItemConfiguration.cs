using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxSavedItemConfiguration : IEntityTypeConfiguration<AidxSavedItem>
{
    public void Configure(EntityTypeBuilder<AidxSavedItem> builder)
    {
        builder.ToTable("AidxSavedItems", "aidx");

        builder.Property(s => s.ContentType).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => new { s.UserId, s.ContentType, s.ContentId }).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
