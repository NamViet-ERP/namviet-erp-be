using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NamViet.ERP.Api.Middleware;
using NamViet.ERP.Api.Services;
using NamViet.ERP.Application.Abstractions.Behaviors;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;
using NamViet.ERP.Modules.Hr.Infrastructure;
using NamViet.ERP.SharedKernel.Interfaces;
using Serilog;

// Alias để tránh nhầm với base class IdentityDbContext
using AppIdentityDbContext = NamViet.ERP.Modules.Identity.Infrastructure.Persistence.IdentityDbContext;
using AppWarehouseDbContext = NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.WarehouseDbContext;
using AppHrDbContext = NamViet.ERP.Modules.Hr.Infrastructure.Persistence.HrDbContext;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

// ── Modules ────────────────────────────────────────────────────────────────
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddWarehouseModule(builder.Configuration);
builder.Services.AddHrModule(builder.Configuration);

// ── MediatR – tất cả assemblies + global behaviors ────────────────────────
builder.Services.AddMediatR(cfg =>
{
    // ApiHost + 3 Infrastructure modules (chứa handlers)
    cfg.RegisterServicesFromAssemblies(
        typeof(Program).Assembly,
        typeof(NamViet.ERP.Modules.Identity.Infrastructure.IdentityModule).Assembly,
        typeof(NamViet.ERP.Modules.Warehouse.Infrastructure.WarehouseModule).Assembly,
        typeof(NamViet.ERP.Modules.Hr.Infrastructure.HrModule).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

// ── Current user ───────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddSingleton<NamViet.ERP.Api.Services.Storage.IFileStorage, NamViet.ERP.Api.Services.Storage.LocalFileStorage>();

// ── CORS ───────────────────────────────────────────────────────────────────
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// ── Controllers & Swagger ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NamViet ERP API",
        Version = "v1",
        Description = "Backend API for NamViet ERP – Warehouse & HR Management"
    });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token: Bearer {token}"
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Health check ───────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Auto-migrate + seed ────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        await services.GetRequiredService<AppIdentityDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AppWarehouseDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AppHrDbContext>().Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var hrDb = services.GetRequiredService<AppHrDbContext>();
        await IdentitySeeder.SeedAsync(roleManager, userManager, app.Configuration, logger);
        await IdentitySeeder.SeedDemoMembersAsync(userManager, hrDb, logger);
        await WmsSeeder.SeedAsync(services.GetRequiredService<AppWarehouseDbContext>(), logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi khởi tạo database.");
        throw;
    }
}

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NamViet ERP v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseSerilogRequestLogging();
app.UseCors();

var webRoot = app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "hr"));
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
