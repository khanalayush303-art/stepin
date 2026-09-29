using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Companies;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Website).HasMaxLength(300);
        builder.Property(c => c.LogoUrl).HasMaxLength(500);
        builder.Property(c => c.Industry).HasMaxLength(100);
        builder.Property(c => c.Location).HasMaxLength(200);
    }
}
