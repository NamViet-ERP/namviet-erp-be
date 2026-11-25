using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Controllers.Hr;
using NamViet.ERP.Api.Controllers.Purchasing;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Security;

public sealed class BackendHardeningPhase1Tests
{
    public static TheoryData<Type, string, string[]> AnyPermissionActions => new()
    {
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.List),
            ["wms.projects.view:all", "wms.materials.view:all", "purchasing.plans.view"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.GetById),
            ["wms.projects.view:all", "wms.materials.view:all", "purchasing.plans.view"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.DownloadImportTemplate),
            ["wms.projects.view:all", "wms.materials.view:all", "purchasing.plans.view"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.Create),
            ["wms.materials.create", "wms.projects.update", "purchasing.plans.create"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.ParseImportFile),
            ["wms.materials.create", "wms.projects.update", "purchasing.plans.create"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.ImportBatch),
            ["wms.materials.create", "wms.projects.update", "purchasing.plans.create"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.Update),
            ["wms.materials.update", "wms.projects.update", "purchasing.plans.update"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.ToggleActive),
            ["wms.materials.update", "wms.projects.update", "purchasing.plans.update"]
        },
        {
            typeof(ServiceExpenseItemsController),
            nameof(ServiceExpenseItemsController.Delete),
            ["wms.materials.delete", "wms.projects.update", "purchasing.plans.update"]
        },
        {
            typeof(ServiceExpenseCategoriesController),
            nameof(ServiceExpenseCategoriesController.GetAll),
            ["wms.projects.view:all", "wms.materials.view:all", "purchasing.plans.view"]
        },
        {
            typeof(BranchesController),
            nameof(BranchesController.GetAll),
            ["hrm.members.settings.view", "hrm.employees.view:all", "hrm.employees.view:own"]
        },
        {
            typeof(BranchesController),
            nameof(BranchesController.GetById),
            ["hrm.members.settings.view", "hrm.employees.view:all", "hrm.employees.view:own"]
        },
    };

    [Theory]
    [MemberData(nameof(AnyPermissionActions))]
    public void Controller_action_enforces_catalogued_any_permissions_without_allow_anonymous(
        Type controller,
        string actionName,
        string[] expectedPermissions)
    {
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());

        var method = controller.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));

        var attr = method.GetCustomAttribute<RequireAnyPermissionAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(PermissionPolicies.AnyName(expectedPermissions), attr.Policy);

        foreach (var permission in expectedPermissions)
            Assert.True(PermissionCatalog.IsValid(permission), $"Permission '{permission}' must be catalogued.");
    }

    [Fact]
    public void ServiceExpenseItemsController_has_no_unprotected_actions_or_allow_anonymous()
    {
        var actions = typeof(ServiceExpenseItemsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

        foreach (var action in actions)
        {
            Assert.Empty(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
            var hasAny = action.GetCustomAttribute<RequireAnyPermissionAttribute>() is not null;
            var hasSingle = action.GetCustomAttribute<RequirePermissionAttribute>() is not null;
            Assert.True(hasAny || hasSingle, $"Action {action.Name} must declare permission requirement.");
        }
    }

    [Theory]
    [InlineData("evil.html", "text/html")]
    [InlineData("evil.htm", "application/octet-stream")]
    [InlineData("xss.svg", "image/svg+xml")]
    [InlineData("payload.js", "application/javascript")]
    [InlineData("malware.exe", "application/x-msdownload")]
    [InlineData("run.bat", "text/plain")]
    [InlineData("script.sh", "text/plain")]
    [InlineData("script.ps1", "text/plain")]
    public async Task ProjectDocuments_UploadFile_RejectsDangerousExtensions(string fileName, string contentType)
    {
        await using var db = CreateWarehouseDb();
        var (controller, docId, storage) = await CreateDocumentControllerAsync(db);

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("test payload"));
        var formFile = CreateFormFile(ms, fileName, contentType);

        var result = await controller.UploadFile("PRJ-SEC", docId, formFile, storage);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, storage.SaveCalls);
    }

    [Fact]
    public async Task ProjectDocuments_UploadFile_RejectsSpoofedMagicBytesAndDangerousMime()
    {
        await using var db = CreateWarehouseDb();
        var (controller, docId, storage) = await CreateDocumentControllerAsync(db);

        // Spoofed PDF containing HTML script
        using (var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>")))
        {
            var res = await controller.UploadFile(
                "PRJ-SEC",
                docId,
                CreateFormFile(htmlStream, "contract.pdf", "application/pdf"),
                storage);
            Assert.IsType<BadRequestObjectResult>(res.Result);
        }

        // Spoofed PNG with plain text instead of PNG magic header
        using (var fakePngStream = new MemoryStream(Encoding.UTF8.GetBytes("<html>not a png</html>")))
        {
            var res = await controller.UploadFile(
                "PRJ-SEC",
                docId,
                CreateFormFile(fakePngStream, "diagram.png", "image/png"),
                storage);
            Assert.IsType<BadRequestObjectResult>(res.Result);
        }

        // Plain .txt disguising an <svg> payload
        using (var svgTextStream = new MemoryStream(Encoding.UTF8.GetBytes("<svg onload=alert(1)>")))
        {
            var res = await controller.UploadFile(
                "PRJ-SEC",
                docId,
                CreateFormFile(svgTextStream, "notes.txt", "text/plain"),
                storage);
            Assert.IsType<BadRequestObjectResult>(res.Result);
        }

        // Valid PDF header paired with forbidden HTML MIME
        byte[] validPdfBytes = "%PDF-1.7\n1 0 obj\n<<>>\nendobj\n"u8.ToArray();
        using (var mimeStream = new MemoryStream(validPdfBytes))
        {
            var res = await controller.UploadFile(
                "PRJ-SEC",
                docId,
                CreateFormFile(mimeStream, "contract.pdf", "text/html"),
                storage);
            Assert.IsType<BadRequestObjectResult>(res.Result);
        }

        Assert.Equal(0, storage.SaveCalls);
    }

    [Fact]
    public async Task ProjectDocuments_UploadFile_AcceptsValidPdfAndPng_AndSavesFullStream()
    {
        await using var db = CreateWarehouseDb();
        var (controller, docId, storage) = await CreateDocumentControllerAsync(db);

        byte[] pdfBytes = "%PDF-1.7\n%âãÏÓ\n1 0 obj\n<< /Type /Catalog >>\nendobj\n"u8.ToArray();
        using (var pdfStream = new MemoryStream(pdfBytes))
        {
            var pdfResult = await controller.UploadFile(
                "PRJ-SEC",
                docId,
                CreateFormFile(pdfStream, "spec.pdf", "application/pdf"),
                storage);

            var ok = Assert.IsType<OkObjectResult>(pdfResult.Result);
            var dto = Assert.IsType<ProjectDocumentDto>(ok.Value);
            Assert.Equal("spec.pdf", dto.FileName);
            Assert.Equal(pdfBytes, storage.LastSavedBytes);
        }

        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        using (var pngStream = new MemoryStream(pngBytes))
        {
            var pngResult = await controller.UploadFile(
                "PRJ-SEC",
                docId,
                CreateFormFile(pngStream, "drawing.png", "image/png"),
                storage);

            var ok = Assert.IsType<OkObjectResult>(pngResult.Result);
            var dto = Assert.IsType<ProjectDocumentDto>(ok.Value);
            Assert.Equal("drawing.png", dto.FileName);
            Assert.Equal(pngBytes, storage.LastSavedBytes);
        }

        Assert.Equal(2, storage.SaveCalls);
    }

    [Fact]
    public async Task StockController_OpeningBalance_RejectsNonAdminWithAdminUsersViewPermission()
    {
        await using var db = CreateWarehouseDb();
        var nonAdminUser = new ConfigurableCurrentUser(
            roles: [RolePermissionTemplates.HrRole],
            permissions: ["admin.users.view:all", "wms.stock.view:all"]);

        var controller = new StockController(db, null!, nonAdminUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Role, RolePermissionTemplates.HrRole),
                        new Claim("permission", "admin.users.view:all"),
                    ],
                    "Test"))
                }
            }
        };

        // Also assert declarative [Authorize(Roles = "Admin")] gates on both endpoints.
        var previewAttr = typeof(StockController)
            .GetMethod(nameof(StockController.PreviewOpeningBalance))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var importAttr = typeof(StockController)
            .GetMethod(nameof(StockController.ImportOpeningBalance))!
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(RolePermissionTemplates.AdminRole, previewAttr?.Roles);
        Assert.Equal(RolePermissionTemplates.AdminRole, importAttr?.Roles);

        using var ms = BuildMinimalOpeningStockExcelStream();
        var formFile = CreateFormFile(
            ms,
            "opening.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var previewResult = await controller.PreviewOpeningBalance(formFile, null, default);
        Assert.IsType<ForbidResult>(previewResult.Result);

        ms.Position = 0;
        var importResult = await controller.ImportOpeningBalance(formFile, null, default);
        Assert.IsType<ForbidResult>(importResult.Result);
    }

    [Fact]
    public async Task StockController_OpeningBalance_AllowsTrueAdminUser()
    {
        await using var db = CreateWarehouseDb();
        var adminUser = new ConfigurableCurrentUser(
            roles: [RolePermissionTemplates.AdminRole],
            permissions: ["admin.users.view:all", "wms.stock.view:all"]);

        var controller = new StockController(db, null!, adminUser);
        using var ms = BuildMinimalOpeningStockExcelStream();
        var formFile = CreateFormFile(
            ms,
            "opening.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var previewResult = await controller.PreviewOpeningBalance(formFile, null, default);
        Assert.IsType<OkObjectResult>(previewResult.Result);
    }

    [Fact]
    public async Task CskhMasterDataController_GetMasterData_ReturnsEmptyStoreWithoutInsertingDbRows()
    {
        await using var db = CreateWarehouseDb();
        var controller = new CskhMasterDataController(db);

        var result = await controller.GetMasterData(default);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);

        using var doc = JsonDocument.Parse(content.Content!);
        foreach (var key in new[] { "industries", "boilerTypes", "statuses", "priorities", "phases", "schedules" })
        {
            Assert.True(doc.RootElement.TryGetProperty(key, out var prop));
            Assert.Equal(JsonValueKind.Array, prop.ValueKind);
            Assert.Equal(0, prop.GetArrayLength());
        }

        Assert.Equal(0, await db.WmsSettings.CountAsync());
    }

    [Fact]
    public void FileStorageProviderSelection_RespectsLocalForceAndS3Configuration()
    {
        var s3WithLocalForce = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:Provider"] = "LocalForce",
                ["FileStorage:S3:BucketName"] = "erp-bucket",
                ["FileStorage:S3:ServiceUrl"] = "https://s3.example.com",
                ["FileStorage:S3:AccessKey"] = "key",
                ["FileStorage:S3:SecretKey"] = "secret",
            })
            .Build();
        Assert.False(FileStorageProviderSelection.Resolve(s3WithLocalForce).UseS3);

        var autoS3Config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:S3:BucketName"] = "erp-bucket",
                ["FileStorage:S3:ServiceUrl"] = "https://s3.example.com",
                ["FileStorage:S3:AccessKey"] = "key",
                ["FileStorage:S3:SecretKey"] = "secret",
            })
            .Build();
        var (useS3, s3Options) = FileStorageProviderSelection.Resolve(autoS3Config);
        Assert.True(useS3);
        Assert.Equal("erp-bucket", s3Options.BucketName);
    }

    private static WarehouseDbContext CreateWarehouseDb() =>
        new(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(ProjectDocumentsController Controller, string DocId, CapturingFileStorage Storage)>
        CreateDocumentControllerAsync(WarehouseDbContext db)
    {
        db.Projects.Add(new Project { ProjectCode = "PRJ-SEC", ProjectName = "Security Test Project" });
        await db.SaveChangesAsync();

        var service = new CskhCoordinationService(db);
        var doc = await service.CreateDocumentAsync(
            "PRJ-SEC",
            new CreateProjectDocumentRequest("DOC-SEC", "Security Document", "other", null, null));

        return (new ProjectDocumentsController(service), doc.Id, new CapturingFileStorage());
    }

    private static FormFile CreateFormFile(MemoryStream ms, string fileName, string contentType)
    {
        ms.Position = 0;
        return new FormFile(ms, 0, ms.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static MemoryStream BuildMinimalOpeningStockExcelStream()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("TonDauKy");
        ws.Cell(1, 1).Value = "Mã vật tư";
        ws.Cell(1, 2).Value = "Tên vật tư";
        ws.Cell(1, 3).Value = "Số lượng tồn";
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private sealed class CapturingFileStorage : IFileStorage
    {
        public int SaveCalls { get; private set; }
        public byte[] LastSavedBytes { get; private set; } = [];

        public async Task<StoredFile> SaveAsync(
            Stream content,
            string relativePath,
            string contentType,
            CancellationToken ct = default)
        {
            SaveCalls++;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            LastSavedBytes = copy.ToArray();
            return new StoredFile(relativePath, Path.GetFileName(relativePath), LastSavedBytes.Length);
        }

        public StoredFileContent? Get(string storageKey) => null;
        public Task<StoredFileStream?> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<StoredFileStream?>(null);
        public Task<int> DeleteUnreferencedOlderThanAsync(IReadOnlySet<string> referencedKeys, DateTime olderThanUtc, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class ConfigurableCurrentUser(string[] roles, string[] permissions) : ICurrentUser
    {
        private readonly ClaimsPrincipal _principal = new();

        public Guid Id => Guid.Parse("22222222-2222-2222-2222-222222222222");
        public string Name => "Test Actor";
        public string Email => "actor@mayzer.com";
        public string EmployeeCode => "EMP-SEC";
        public int? EmployeeId => 2;
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; } = roles;
        public IReadOnlyList<string> Permissions { get; } = permissions;
        public ClaimsPrincipal Principal => _principal;

        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);
        public bool HasAllPermissions(params string[] required) => required.All(HasPermission);
        public bool HasAnyPermission(params string[] required) => required.Any(HasPermission);
    }
}
