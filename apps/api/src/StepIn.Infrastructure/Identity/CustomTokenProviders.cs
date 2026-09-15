using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StepIn.Infrastructure.Identity;

// Identity's DataProtectorTokenProvider<TUser> resolves its options via plain
// IOptions<T>, which is always the single unnamed instance — registering the
// same provider type twice under different *names* would have both share one
// TokenLifespan. Distinct *option types* (per Microsoft's documented pattern
// for per-purpose token lifespans) are what actually give email confirmation
// and password reset their own expiries.

public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions;

public sealed class EmailConfirmationTokenProvider<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<EmailConfirmationTokenProviderOptions> options,
    ILogger<EmailConfirmationTokenProvider<TUser>> logger)
    : DataProtectorTokenProvider<TUser>(dataProtectionProvider, options, logger)
    where TUser : class;

public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions;

public sealed class PasswordResetTokenProvider<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<PasswordResetTokenProvider<TUser>> logger)
    : DataProtectorTokenProvider<TUser>(dataProtectionProvider, options, logger)
    where TUser : class;
