using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.ClerkUserId).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.AccountStatus).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(u => u.ClerkUserId).IsUnique();

        // Not unique: Clerk's default session token carries no email claim at
        // all (that needs a custom session token configured in the Clerk
        // Dashboard), so every new user syncs with Email = "" until that's
        // set up — a unique index here means the second such user's very
        // first request crashes on a duplicate-key violation. ClerkUserId
        // is, and remains, the only identity guarantee this table makes.
        builder.HasIndex(u => u.Email);
    }
}
