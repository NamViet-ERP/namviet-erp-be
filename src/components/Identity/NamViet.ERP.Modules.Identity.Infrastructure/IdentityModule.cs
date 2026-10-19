using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Application.Abstractions;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using System.Text;

namespace NamViet.ERP.Modules.Identity.Infrastructure;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContext<IdentityDbContext>(opt =>
            opt.UseNpgsql(connectionString, b => b.MigrationsHistoryTable("__ef_migrations_identity", "identity")));

        services.AddIdentity<ApplicationUser, ApplicationRole>(opt =>
        {
            opt.Password.RequireDigit = true;
            opt.Password.RequiredLength = 8;
            opt.Password.RequireUppercase = true;
            opt.Password.RequireNonAlphanumeric = false;
            opt.User.RequireUniqueEmail = true;
            IdentitySecurityOptions.ConfigureLockout(opt);
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddErrorDescriber<VietnameseIdentityErrorDescriber>()
        .AddDefaultTokenProviders();

        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddScoped<IRefreshTokenRevoker, RefreshTokenRevoker>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<NamViet.ERP.SharedKernel.Interfaces.IAccountStatusProvider, AccountStatusProvider>();

        // MediatR assembly được đăng ký tập trung ở Program.cs
        // services.AddMediatR(...) không gọi ở đây

        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is required.");

        services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.Zero
            };
            opt.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Access token chỉ nhận qua Authorization header. Ngoại lệ duy nhất:
                    // SignalR hub (WebSocket không gửi được header) đọc query ?access_token=.
                    // Nhánh đọc cookie nv_access_token legacy đã xóa - cookie không còn được set,
                    // giữ lại chỉ mở rủi ro CSRF nếu có nơi set lại.
                    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                    var hasBearer = !string.IsNullOrEmpty(authHeader)
                        && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

                    if (!hasBearer)
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
}
