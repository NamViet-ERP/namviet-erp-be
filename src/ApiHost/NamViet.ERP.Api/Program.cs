using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using NamViet.ERP.Api.Application.Services.Admin;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Api.Infrastructure;
using NamViet.ERP.Api.Middleware;
using NamViet.ERP.Api.Services;
using NamViet.ERP.Application.Abstractions.Behaviors;
using NamViet.ERP.Modules.Identity.Infrastructure;
using NamViet.ERP.Modules.Warehouse.Infrastructure;
using NamViet.ERP.Modules.Hr.Infrastructure;
using NamViet.ERP.SharedKernel.Interfaces;
using Serilog;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

ConfigurationSecurityValidator.Validate(builder.Configuration, builder.Environment);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

// ── Modules ────────────────────────────────────────────────────────────────
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddPermissionAuthorization();
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

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssembly(
    typeof(NamViet.ERP.Modules.Identity.Application.Commands.LoginCommand).Assembly);

// ── Rate limiting ──────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = 429,
            title = "Quá nhiều yêu cầu",
            detail = "Vui lòng thử lại sau vài phút.",
        }, ct);
    };

    static string ClientIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy(RateLimitPolicies.Auth, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        }));

    options.AddPolicy(RateLimitPolicies.PublicWrite, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
        }));

    options.AddPolicy(RateLimitPolicies.PublicUpload, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
        }));

    options.AddPolicy(RateLimitPolicies.PublicRead, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
        }));
});

// ── Current user ───────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddSingleton<NamViet.ERP.Api.Infrastructure.Auth.IAuthCookieService, NamViet.ERP.Api.Infrastructure.Auth.AuthCookieService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IWmsCatalogQueryService, NamViet.ERP.Api.Application.Services.Wms.WmsCatalogQueryService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IWmsVoucherExcelExportService, NamViet.ERP.Api.Application.Services.Wms.WmsVoucherExcelExportService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IWmsReportService, NamViet.ERP.Api.Application.Services.Wms.WmsReportService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Hr.IRecruitmentAnalyticsService, NamViet.ERP.Api.Application.Services.Hr.RecruitmentAnalyticsService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IWmsNotificationService, NamViet.ERP.Api.Application.Services.Wms.WmsNotificationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IWmsLowStockAlertService, NamViet.ERP.Api.Application.Services.Wms.WmsLowStockAlertService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IProjectBudgetService, NamViet.ERP.Api.Application.Services.Wms.ProjectBudgetService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.ProjectWarehouseAccessService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IStocktakeFreezeGuard, NamViet.ERP.Api.Application.Services.Wms.StocktakeFreezeGuard>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IImportApplicationService, NamViet.ERP.Api.Application.Services.Wms.ImportApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IImportExcelService, NamViet.ERP.Api.Application.Services.Wms.ImportExcelService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IExportApplicationService, NamViet.ERP.Api.Application.Services.Wms.ExportApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.ITransferApplicationService, NamViet.ERP.Api.Application.Services.Wms.TransferApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IExportReturnApplicationService, NamViet.ERP.Api.Application.Services.Wms.ExportReturnApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IStocktakeApplicationService, NamViet.ERP.Api.Application.Services.Wms.StocktakeApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePlanCodeGenerator, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePlanCodeGenerator>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseOrderCodeGenerator, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseOrderCodeGenerator>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePlanImportService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePlanImportService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePlanService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePlanService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseOrderReceiptService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseOrderReceiptService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseDefectApplicationService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseDefectApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseOrderService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseOrderService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePaymentCodeGenerator, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePaymentCodeGenerator>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePaymentService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePaymentService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasingDashboardService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasingDashboardService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Shared.IActivityEventWriter, NamViet.ERP.Api.Application.Services.Shared.ActivityEventWriter>();
builder.Services.AddScoped<IAdminUserQueryService, AdminUserQueryService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Hr.IEmployeeAccessService, NamViet.ERP.Api.Application.Services.Hr.EmployeeAccessService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Hr.IEmployeeOnboardingInviteService, NamViet.ERP.Api.Application.Services.Hr.EmployeeOnboardingInviteService>();
builder.Services.AddSingleton<NamViet.ERP.Api.Services.Storage.IFileStorage, NamViet.ERP.Api.Services.Storage.LocalFileStorage>();
builder.Services.AddHostedService<NamViet.ERP.Api.Application.Services.Hr.HrPrivateUploadCleanupService>();
builder.Services.AddSignalR();

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
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = ApiValidationResponses.InvalidModelState;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NamViet ERP API",
        Version = "v1",
        Description = "Backend API for NamViet ERP"
    });
    // Emit accurate nullability from C# NRT so FE OpenAPI codegen gets required vs optional right.
    opt.SupportNonNullableReferenceTypes();
    opt.NonNullableReferenceTypesAsRequired();
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

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/uploads"),
    branch => branch.UseStaticFiles());

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<NamViet.ERP.Api.Hubs.ActivityHub>("/hubs/activity");
app.MapHealthChecks("/health");

app.Run();
