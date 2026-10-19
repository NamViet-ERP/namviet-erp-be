using Microsoft.AspNetCore.Identity;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public static class IdentitySecurityOptions
{
    public static void ConfigureLockout(IdentityOptions options)
    {
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }
}
