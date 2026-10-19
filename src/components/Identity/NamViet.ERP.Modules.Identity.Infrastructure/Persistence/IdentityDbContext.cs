using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identity");

        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.EmployeeCode).HasMaxLength(50).IsRequired();
            e.Property(u => u.Name).HasMaxLength(255).IsRequired();
            e.HasIndex(u => u.EmployeeCode).IsUnique();
        });

        builder.Entity<ApplicationRole>(e =>
        {
            e.ToTable("roles");
            e.Property(r => r.Description).HasMaxLength(255);
            e.Property(r => r.Module).HasMaxLength(50).HasDefaultValue("General");
        });

        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.Token).HasMaxLength(512);
            e.Property(t => t.TokenHash).HasMaxLength(64);
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
