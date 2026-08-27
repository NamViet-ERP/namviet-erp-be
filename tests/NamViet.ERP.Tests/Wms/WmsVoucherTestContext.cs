using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Wms;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid Id { get; init; } = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    public string Email => "keeper@namviet.vn";
    public string Name => "Test Keeper";
    public string EmployeeCode => "NV-TEST";
    public int? EmployeeId => null;
    public IReadOnlyList<string> Roles => ["Warehouse_Keeper"];
    public IReadOnlyList<string> Permissions => [];
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

internal sealed class FakeProjectBudgetService : IProjectBudgetService
{
    public Task<decimal> GetApprovedImportValueAsync(string projectCode, CancellationToken ct = default) =>
        Task.FromResult(0m);

    public Task<IReadOnlyDictionary<string, decimal>> GetApprovedImportValuesAsync(
        IReadOnlyList<string> projectCodes, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>());

    public decimal SumImportValue(WarehouseTransaction transaction) => 0m;

    public Task EnsureImportApprovalWithinBudgetAsync(
        WarehouseTransaction transaction, bool confirmOverBudget, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal sealed class FakeFileStorage : IFileStorage
{
    public Task<StoredFile> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken ct = default) =>
        Task.FromResult(new StoredFile(relativePath, Path.GetFileName(relativePath), 0));

    public StoredFileContent? Get(string storageKey) => null;

    public Task<int> DeleteUnreferencedOlderThanAsync(
        IReadOnlySet<string> referencedKeys, DateTime olderThanUtc, CancellationToken ct = default) =>
        Task.FromResult(0);
}

internal sealed class FakeActivityEventWriter : IActivityEventWriter
{
    public Task WriteAsync(ActivityEventWriteRequest request, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal sealed class FakePurchaseWarrantyService : IPurchaseWarrantyService
{
    public Task<IReadOnlyList<PurchaseWarrantyDto>> ListAsync(
        string? projectCode, string? materialCode, int? supplierId, string? status, string? search,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PurchaseWarrantyDto>>([]);

    public Task<PurchaseWarrantyDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<PurchaseWarrantyDetailDto?>(null);

    public Task<PurchaseWarrantyDetailDto?> UpdateAsync(
        Guid id, UpdatePurchaseWarrantyRequest request, CancellationToken ct = default) =>
        Task.FromResult<PurchaseWarrantyDetailDto?>(null);

    public Task<IReadOnlyList<PurchaseWarrantyClaimListDto>> ListClaimsAsync(
        string? kind, string? status, string? search, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PurchaseWarrantyClaimListDto>>([]);

    public Task<PurchaseWarrantyClaimDto> CreateClaimAsync(
        Guid warrantyId, CreatePurchaseWarrantyClaimRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PurchaseWarrantyClaimDto?> SubmitClaimAsync(Guid claimId, CancellationToken ct = default) =>
        Task.FromResult<PurchaseWarrantyClaimDto?>(null);

    public Task<PurchaseWarrantyClaimDto?> CloseClaimAsync(
        Guid claimId, ClosePurchaseWarrantyClaimRequest request, CancellationToken ct = default) =>
        Task.FromResult<PurchaseWarrantyClaimDto?>(null);

    public Task UpsertFromApprovedImportAsync(WarehouseTransaction import, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<SyncPurchaseWarrantiesResult> SyncFromReceivedPoLinesAsync(CancellationToken ct = default) =>
        Task.FromResult(new SyncPurchaseWarrantiesResult(0, 0));
}

internal sealed class FakePurchaseDefectApplicationService : IPurchaseDefectApplicationService
{
    public Task<List<PurchaseDefectDto>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult(new List<PurchaseDefectDto>());

    public Task<PurchaseDefectDto?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectDto?>(null);

    public Task<IReadOnlyList<PoDefectEligiblePurchaseOrderDto>> ListEligiblePurchaseOrdersAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PoDefectEligiblePurchaseOrderDto>>([]);

    public Task<PoDefectPreviewDto> PreviewFromPoAsync(string poCode, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PurchaseDefectDto> CreateFromPoAsync(
        string poCode, CreateDefectFromPoRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<StockDefectPreviewDto> PreviewFromStockAsync(string projectCode, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PurchaseDefectDto> CreateFromStockAsync(
        CreateDefectFromStockRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<string?> CreateAndApproveFromImportAsync(
        WarehouseTransaction import, string? defectReason, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    public Task<PurchaseDefectDto?> SubmitAsync(string id, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectDto?>(null);

    public Task<PurchaseDefectDto?> ApproveAsync(string id, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectDto?>(null);

    public Task<PurchaseDefectDto?> UpdateReasonAsync(string id, string? reason, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectDto?>(null);

    public Task<PurchaseDefectDto?> UpdateItemsAsync(
        string id, UpdateDefectItemsRequest request, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectDto?>(null);

    public Task<PurchaseDefectDto?> CancelAsync(string id, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectDto?>(null);

    public Task<PurchaseDefectDto?> UpdateProcessingAsync(
        string id, UpdateDefectProcessingRequest request, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectDto?>(null);

    public Task<PurchaseDefectAttachmentDto?> UploadAttachmentAsync(
        string id, string kind, string fileName, string contentType, Stream content, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectAttachmentDto?>(null);

    public Task<PurchaseDefectAttachmentDto?> GetAttachmentAsync(
        string id, Guid attachmentId, CancellationToken ct = default) =>
        Task.FromResult<PurchaseDefectAttachmentDto?>(null);

    public Task<bool> DeleteAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(false);
}

internal sealed class FakeSupplierDeliveryEvaluationService : ISupplierDeliveryEvaluationService
{
    public Task<SupplierDeliveryEvaluationPagedResponse> ListAsync(
        int? supplierId, string? poCode, string? status, string? search,
        int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(new SupplierDeliveryEvaluationPagedResponse([], 0, page, pageSize, 1));

    public Task EnsurePendingFromApprovedImportAsync(
        WarehouseTransaction import, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<SupplierDeliveryEvaluationDto> CompleteAsync(
        int id, CompleteSupplierDeliveryEvaluationRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

internal sealed class FakeWmsCatalogQueryService : IWmsCatalogQueryService
{
    public Task<decimal> GetLatestPriceAsync(string materialCode, CancellationToken ct = default) =>
        Task.FromResult(0m);

    public Task<decimal> GetLatestPriceForSupplierAsync(
        string materialCode, int supplierId, CancellationToken ct = default) =>
        Task.FromResult(0m);

    public Task<(int totalStock, decimal totalValue)> GetWarehouseTotalsAsync(
        string warehouseId, CancellationToken ct = default) =>
        Task.FromResult((0, 0m));

    public Task<decimal> GetTotalInventoryValueAsync(CancellationToken ct = default) =>
        Task.FromResult(0m);
}

internal sealed class WmsVoucherTestContext : IDisposable
{
    public WarehouseDbContext Db { get; }
    public InventoryDomainService Inventory { get; }
    public FakeCurrentUser CurrentUser { get; } = new();
    public ImportApplicationService Imports { get; }
    public ExportApplicationService Exports { get; }
    public TransferApplicationService Transfers { get; }
    public ExportReturnApplicationService ExportReturns { get; }

    public WmsVoucherTestContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"wms-voucher-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new WarehouseDbContext(options);
        Inventory = new InventoryDomainService(Db);
        var stocktakeFreeze = new StocktakeFreezeGuard(Db);
        var fileStorage = new FakeFileStorage();
        Imports = new ImportApplicationService(
            Db, Inventory, new FakeProjectBudgetService(), stocktakeFreeze, CurrentUser,
            new PurchaseOrderReceiptService(Db, new FakePurchaseWarrantyService()),
            new FakePurchaseDefectApplicationService(),
            new FakeSupplierDeliveryEvaluationService(),
            fileStorage, new FakeActivityEventWriter());
        Exports = new ExportApplicationService(
            Db, Inventory, stocktakeFreeze, CurrentUser, new FakeWmsCatalogQueryService());
        Transfers = new TransferApplicationService(Db, Inventory, stocktakeFreeze, CurrentUser);
        ExportReturns = new ExportReturnApplicationService(
            Db, Inventory, stocktakeFreeze, CurrentUser, fileStorage);
        SeedBaseline();
    }

    public void SeedProjectStock(string materialCode, decimal actualQty)
    {
        var existing = Db.ProjectInventories
            .FirstOrDefault(p => p.ProjectCode == "DA-TEST" && p.MaterialCode == materialCode);
        if (existing is null)
        {
            Db.ProjectInventories.Add(new ProjectInventory
            {
                ProjectCode = "DA-TEST",
                MaterialCode = materialCode,
                ActualImportQty = actualQty,
            });
        }
        else
        {
            existing.ActualImportQty = actualQty;
        }
        Db.SaveChanges();
    }

    public void SeedFrozenStocktake(string projectCode = "DA-TEST", string voucherCode = "KK-TEST-FROZEN")
    {
        Db.Stocktakes.Add(new Stocktake
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            VoucherCode = voucherCode,
            VoucherDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedById = CurrentUser.Id,
            CreatedByName = CurrentUser.Name,
            Status = "pending",
            WarehouseId = projectCode,
            WarehouseName = "Dự án test",
            Condition = "counting",
            IsFrozen = true,
            Note = "Test freeze",
        });
        Db.SaveChanges();
    }

    private void SeedBaseline()
    {
        Db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = WmsWarehouseIds.General,
            Code = "KHO-TONG",
            Name = "Kho tổng",
            Group = "Kho tổng",
            Address = "A",
            Branch = "HCM",
            Manager = "Keeper",
            Status = "active",
        });
        Db.WmsSettings.Add(new WmsSetting
        {
            Key = WmsSettingsKeys.GeneralWarehouseId,
            Value = WmsWarehouseIds.General,
        });
        Db.Projects.Add(new Project
        {
            ProjectCode = "DA-TEST",
            ProjectName = "Dự án test",
            PmName = "PM",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ProjectStatus.Active,
        });
        Db.Materials.Add(new Material
        {
            MaterialCode = "MAT-001",
            MaterialName = "Vật tư test",
            CategoryId = 1,
            Unit = "Cái",
        });
        Db.WarehouseInventories.Add(new WarehouseInventory
        {
            WarehouseId = WmsWarehouseIds.General,
            MaterialCode = "MAT-001",
            Qty = 80m,
        });
        Db.SaveChanges();
    }

    public static ImportItemDto ImportLine(decimal qty) =>
        new("MAT-001", "MAT-001", "Vật tư test", qty, "Cái", 1000m);

    public static ExportItemDto ExportLine(decimal qty) =>
        new("MAT-001", "MAT-001", "Vật tư test", qty, "Cái");

    public static TransferItemDto TransferLine(decimal qty) =>
        new("MAT-001", "MAT-001", "Vật tư test", qty, "Cái");

    public static ExportReturnItemDto ExportReturnLine(decimal qty) =>
        new("MAT-001", "MAT-001", "Vật tư test", qty, "Cái");

    /** Seeds receipt evaluation + photo + document required before import approve. */
    public async Task SeedImportApprovalEvidenceAsync(string importId, CancellationToken ct = default)
    {
        await Imports.UpdateReceiptEvaluationAsync(importId, "Hàng đúng số lượng, OK", ct);
        await using (var photo = new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9]))
        {
            await Imports.UploadAttachmentAsync(importId, "photo", "receipt.jpg", "image/jpeg", photo, ct);
        }

        await using (var doc = new MemoryStream([0x25, 0x50, 0x44, 0x46]))
        {
            await Imports.UploadAttachmentAsync(importId, "document", "invoice.pdf", "application/pdf", doc, ct);
        }
    }

    public void Dispose() => Db.Dispose();
}
