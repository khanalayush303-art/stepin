using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxCollaborationEnquiryConfiguration : IEntityTypeConfiguration<AidxCollaborationEnquiry>
{
    public void Configure(EntityTypeBuilder<AidxCollaborationEnquiry> builder)
    {
        builder.ToTable("AidxCollaborationEnquiries", "aidx");

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(5000).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.ProjectId);

        builder.HasOne<AidxProject>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
