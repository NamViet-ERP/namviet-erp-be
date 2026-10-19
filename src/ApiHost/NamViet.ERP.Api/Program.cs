using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using NamViet.ERP.Api.Application.Services.Admin;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Health;
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

// Container hosts (Render Free) often hit inotify limits when ASP.NET watches appsettings.
// Disable config file reload-on-change before WebApplication.CreateBuilder attaches watchers.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_HOSTBUILDER_RELOADCONFIGONCHANGE")))
{
    Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER_RELOADCONFIGONCHANGE", "false");
}

var builder = WebApplication.CreateBuilder(args);

ConfigurationSecurityValidator.Validate(builder.Configuration, builder.Environment);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteTo.Console());

// ── Forwarded headers (TLS reverse proxy → real client IP for rate limit) ──
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Deploy thật chỉ có đúng 1 lớp proxy (Render). ForwardLimit = 1 nghĩa là chỉ tin
    // entry cuối cùng của X-Forwarded-For (do proxy Render thêm); các entry client tự
    // gửi kèm bị bỏ qua nên không spoof được IP để né rate limit.
    options.ForwardLimit = 1;
    // Behind a trusted edge proxy that is the only path to the app (Docker/K8s/Nginx),
    // clear known lists so X-Forwarded-* from that hop is applied. Restrict further via
    // ForwardedHeaders:KnownProxies if the API is reachable from untrusted networks.
    if (builder.Configuration.GetValue("ForwardedHeaders:ClearKnownLists", true))
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }

    var knownProxies = builder.Configuration["ForwardedHeaders:KnownProxies"];
    if (!string.IsNullOrWhiteSpace(knownProxies))
    {
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        foreach (var raw in knownProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (System.Net.IPAddress.TryParse(raw, out var ip))
                options.KnownProxies.Add(ip);
        }
    }
});

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
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = 429,
            title = "Quá nhiều yêu cầu",
            detail = "Hệ thống đang tiếp nhận nhiều yêu cầu cùng lúc. Vui lòng thử lại sau giây lát.",
        }, ct);
    };

    static string ClientIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
    var authLimit = rateLimitConfig.GetValue<int?>("AuthPermitLimit")
        ?? (builder.Environment.IsDevelopment() ? 200 : 120);
    var publicWriteLimit = rateLimitConfig.GetValue<int?>("PublicWritePermitLimit") ?? 30;
    var publicUploadLimit = rateLimitConfig.GetValue<int?>("PublicUploadPermitLimit") ?? 30;
    var publicReadLimit = rateLimitConfig.GetValue<int?>("PublicReadPermitLimit") ?? 180;

    options.AddPolicy(RateLimitPolicies.Auth, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = authLimit,
            Window = TimeSpan.FromMinutes(1),
        }));

    options.AddPolicy(RateLimitPolicies.PublicWrite, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = publicWriteLimit,
            Window = TimeSpan.FromMinutes(1),
        }));

    options.AddPolicy(RateLimitPolicies.PublicUpload, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = publicUploadLimit,
            Window = TimeSpan.FromHours(1),
        }));

    options.AddPolicy(RateLimitPolicies.PublicRead, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = publicReadLimit,
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
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IMaterialImportExcelService, NamViet.ERP.Api.Application.Services.Wms.MaterialImportExcelService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IExportApplicationService, NamViet.ERP.Api.Application.Services.Wms.ExportApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.ITransferApplicationService, NamViet.ERP.Api.Application.Services.Wms.TransferApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IExportReturnApplicationService, NamViet.ERP.Api.Application.Services.Wms.ExportReturnApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Wms.IStocktakeApplicationService, NamViet.ERP.Api.Application.Services.Wms.StocktakeApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePlanCodeGenerator, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePlanCodeGenerator>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseOrderCodeGenerator, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseOrderCodeGenerator>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePlanImportService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePlanImportService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePlanService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePlanService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISupplierMaterialRiskService, NamViet.ERP.Api.Application.Services.Purchasing.SupplierMaterialRiskService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseDocumentService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseDocumentService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseContractService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseContractService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseWarrantyService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseWarrantyService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseEstimateService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseEstimateService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseRfqService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseRfqService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePriceBookService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePriceBookService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseOrderReceiptService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseOrderReceiptService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseDefectApplicationService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseDefectApplicationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchaseOrderService, NamViet.ERP.Api.Application.Services.Purchasing.PurchaseOrderService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePaymentCodeGenerator, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePaymentCodeGenerator>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePaymentService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePaymentService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasePaymentPlanService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasePaymentPlanService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.IPurchasingDashboardService, NamViet.ERP.Api.Application.Services.Purchasing.PurchasingDashboardService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISupplierModuleSettingsService, NamViet.ERP.Api.Application.Services.Purchasing.SupplierModuleSettingsService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISupplierGroupService, NamViet.ERP.Api.Application.Services.Purchasing.SupplierGroupService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISupplierContactService, NamViet.ERP.Api.Application.Services.Purchasing.SupplierContactService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISupplierEvaluationService, NamViet.ERP.Api.Application.Services.Purchasing.SupplierEvaluationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISupplierDeliveryEvaluationService, NamViet.ERP.Api.Application.Services.Purchasing.SupplierDeliveryEvaluationService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISuppliersDashboardService, NamViet.ERP.Api.Application.Services.Purchasing.SuppliersDashboardService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Purchasing.ISupplierPayableService, NamViet.ERP.Api.Application.Services.Purchasing.SupplierPayableService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Shared.IActivityEventWriter, NamViet.ERP.Api.Application.Services.Shared.ActivityEventWriter>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Hr.IEmployeeAccountLinkSynchronizer, NamViet.ERP.Api.Application.Services.Hr.EmployeeAccountLinkSynchronizer>();
builder.Services.AddScoped<IAdminUserQueryService, AdminUserQueryService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Hr.IEmployeeAccessService, NamViet.ERP.Api.Application.Services.Hr.EmployeeAccessService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Hr.IEmployeeOnboardingInviteService, NamViet.ERP.Api.Application.Services.Hr.EmployeeOnboardingInviteService>();
builder.Services.AddScoped<NamViet.ERP.Api.Application.Services.Cskh.ICskhCoordinationService, NamViet.ERP.Api.Application.Services.Cskh.CskhCoordinationService>();
RegisterFileStorage(builder.Services, builder.Configuration);
builder.Services.AddHostedService<NamViet.ERP.Api.Application.Services.Hr.EmployeeAccountLinkHostedService>();
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
        Title = "Mayzer ERP API",
        Version = "v1",
        Description = "Enterprise Backend API for Mayzer ERP System"
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

// ── Health checks ──────────────────────────────────────────────────────────
// /health        → liveness (process up; no dependency checks)
// /health/ready  → readiness (PostgreSQL + file storage writable)
builder.Services.AddHealthChecks()
    .AddCheck<PostgresReadyHealthCheck>("postgres", tags: ["ready"])
    .AddCheck<FileStorageReadyHealthCheck>("file_storage", tags: ["ready"]);

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────
// Must run before rate limiting / auth so RemoteIpAddress reflects X-Forwarded-For.
app.UseForwardedHeaders();

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

app.MapHealthChecks("/health", new HealthCheckOptions
{
    // Liveness: do not run dependency checks (orchestrator restart loop safe).
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    // Chỉ trả trạng thái tổng quát + tên check. Không trả exception/description để
    // tránh lộ thông tin hạ tầng (host/user DB) ra endpoint công khai.
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
            }),
        });
    },
});

app.Run();

static void RegisterFileStorage(IServiceCollection services, IConfiguration configuration)
{
    var provider = configuration["FileStorage:Provider"]?.Trim();

    var s3Options = configuration.GetSection(NamViet.ERP.Api.Services.Storage.S3FileStorageOptions.SectionName)
                        .Get<NamViet.ERP.Api.Services.Storage.S3FileStorageOptions>()
                    ?? new NamViet.ERP.Api.Services.Storage.S3FileStorageOptions();

    var hasS3Credentials = !string.IsNullOrWhiteSpace(s3Options.BucketName)
                           && !string.IsNullOrWhiteSpace(s3Options.ServiceUrl)
                           && !string.IsNullOrWhiteSpace(s3Options.AccessKey)
                           && !string.IsNullOrWhiteSpace(s3Options.SecretKey);

    var useS3 = string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase)
                || (hasS3Credentials && !string.Equals(provider, "LocalForce", StringComparison.OrdinalIgnoreCase));

    if (useS3 && !string.IsNullOrWhiteSpace(s3Options.BucketName))
    {
        services.AddSingleton(s3Options);
        services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
        {
            var s3Config = new Amazon.S3.AmazonS3Config
            {
                ForcePathStyle = s3Options.ForcePathStyle,
            };

            if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
            {
                s3Config.ServiceURL = s3Options.ServiceUrl;
            }

            if (!string.IsNullOrWhiteSpace(s3Options.Region))
            {
                s3Config.AuthenticationRegion = s3Options.Region;
            }

            if (!string.IsNullOrWhiteSpace(s3Options.AccessKey) && !string.IsNullOrWhiteSpace(s3Options.SecretKey))
            {
                var credentials = new Amazon.Runtime.BasicAWSCredentials(s3Options.AccessKey, s3Options.SecretKey);
                return new Amazon.S3.AmazonS3Client(credentials, s3Config);
            }

            return new Amazon.S3.AmazonS3Client(s3Config);
        });

        services.AddSingleton<NamViet.ERP.Api.Services.Storage.IFileStorage, NamViet.ERP.Api.Services.Storage.S3FileStorage>();
    }
    else
    {
        services.AddSingleton<NamViet.ERP.Api.Services.Storage.IFileStorage, NamViet.ERP.Api.Services.Storage.LocalFileStorage>();
    }
}
