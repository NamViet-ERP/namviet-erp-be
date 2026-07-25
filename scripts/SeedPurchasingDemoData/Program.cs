using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

const string SeedMarkerKey = "purchasing_demo_seed_v1";
const string ProjectCode = "DEMO-PUR-PROJ";
const string SupplierCodeA = "NCC-DEMO-A";
const string SupplierCodeB = "NCC-DEMO-B";

const string PlanDraft = "SEED-KHMH-01";
const string PlanActive = "SEED-KHMH-02";
const string PlanClosed = "SEED-KHMH-03";

var demoActorId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
var force = args.Any(a => string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase));

var apiConfigDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ApiHost", "NamViet.ERP.Api"));
var configuration = new ConfigurationBuilder()
    .SetBasePath(apiConfigDir)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets("namviet-erp-api")
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Thiếu ConnectionStrings:Default.");
    Console.Error.WriteLine("Set env ConnectionStrings__Default hoặc user-secrets trên NamViet.ERP.Api.");
    return 1;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddWarehouseModule(configuration);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();

await db.Database.MigrateAsync();

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerKey))
{
    Console.WriteLine($"Marker '{SeedMarkerKey}' đã có — bỏ qua.");
    Console.WriteLine("Dùng --force để chạy lại (upsert theo mã SEED-*).");
    return 0;
}

await using var tx = await db.Database.BeginTransactionAsync();

var project = await EnsureDemoProjectAsync(db);
var (supplierA, supplierB) = await EnsureDemoSuppliersAsync(db);
var today = DateOnly.FromDateTime(DateTime.UtcNow);
var now = DateTime.UtcNow;

var createdPlans = new List<string>();
var createdOrders = new List<string>();
var updatedPlans = new List<string>();
var updatedOrders = new List<string>();

// --- Draft plan: 2 lines chưa mua ---
var draftPlan = await UpsertPlanAsync(
    db, PlanDraft, project.ProjectCode, PurchasePlanStatuses.Draft,
    "Seed demo — kế hoạch nháp", demoActorId, now, createdPlans, updatedPlans);
await UpsertPlanLinesAsync(db, draftPlan, new[]
{
    LineSpec(1, "Thép phi 16", "Kg", 200m, 18_500m, PurchaseLineStatuses.NotPurchased),
    LineSpec(2, "Xi măng PCB40", "Bao", 80m, 95_000m, PurchaseLineStatuses.NotPurchased),
});

// --- Active plan: line statuses + 6 POs ---
var activePlan = await UpsertPlanAsync(
    db, PlanActive, project.ProjectCode, PurchasePlanStatuses.Active,
    "Seed demo — kế hoạch đang mở (đủ trạng thái)", demoActorId, now, createdPlans, updatedPlans);

var activeLines = await UpsertPlanLinesAsync(db, activePlan, new[]
{
    LineSpec(1, "Ống PVC D63", "M", 100m, 42_000m, PurchaseLineStatuses.NotPurchased),
    LineSpec(2, "Cát vàng", "Khối", 50m, 280_000m, PurchaseLineStatuses.NotPurchased,
        proposalUnit: 275_000m, proposalSupplier: supplierA.SupplierName),
    LineSpec(3, "Sơn nước nội thất", "Thùng", 40m, 650_000m, PurchaseLineStatuses.Ordered,
        orderedQty: 40, receivedQty: 0,
        purchaseUnit: 640_000m, purchaseSupplier: supplierA.SupplierName,
        orderedDate: today.AddDays(-10)),
    LineSpec(4, "Gạch ốp lát 60x60", "Thùng", 120m, 185_000m, PurchaseLineStatuses.PartialDelivery,
        orderedQty: 120, receivedQty: 50, defectQty: 2,
        purchaseUnit: 180_000m, purchaseSupplier: supplierB.SupplierName,
        orderedDate: today.AddDays(-20), expected: today.AddDays(-5), receipt: today.AddDays(-3)),
    LineSpec(5, "Thép hình V50", "Kg", 300m, 22_000m, PurchaseLineStatuses.Delivered,
        orderedQty: 300, receivedQty: 300,
        purchaseUnit: 21_500m, purchaseSupplier: supplierA.SupplierName,
        orderedDate: today.AddDays(-40), expected: today.AddDays(-25), receipt: today.AddDays(-24)),
    LineSpec(6, "Dây điện CV 2.5", "Cuộn", 60m, 320_000m, PurchaseLineStatuses.PartialDelivery,
        orderedQty: 60, receivedQty: 25,
        purchaseUnit: 310_000m, purchaseSupplier: supplierB.SupplierName,
        orderedDate: today.AddDays(-45), expected: today.AddDays(-30), receipt: today.AddDays(-28)),
});

await db.SaveChangesAsync();
var lineByNo = activeLines.ToDictionary(l => l.LineNo);

await UpsertOrderAsync(db, "SEED-PO-01", activePlan, lineByNo[1], supplierA,
    PurchaseOrderApprovalStatuses.Draft, PurchaseOrderPlacementStatuses.NotPlaced,
    PurchaseOrderDeliveryStatuses.None, PurchaseOrderPaymentStatuses.Unpaid,
    paymentWarning: false, today.AddDays(-2), paymentDue: null,
    received: 0m, demoActorId, now, createdOrders, updatedOrders);

await UpsertOrderAsync(db, "SEED-PO-02", activePlan, lineByNo[2], supplierA,
    PurchaseOrderApprovalStatuses.Approved, PurchaseOrderPlacementStatuses.NotPlaced,
    PurchaseOrderDeliveryStatuses.None, PurchaseOrderPaymentStatuses.Unpaid,
    paymentWarning: false, today.AddDays(-5), paymentDue: today.AddDays(25),
    received: 0m, demoActorId, now, createdOrders, updatedOrders, approved: true);

await UpsertOrderAsync(db, "SEED-PO-03", activePlan, lineByNo[3], supplierA,
    PurchaseOrderApprovalStatuses.Approved, PurchaseOrderPlacementStatuses.Placed,
    PurchaseOrderDeliveryStatuses.None, PurchaseOrderPaymentStatuses.Unpaid,
    paymentWarning: false, today.AddDays(-10), paymentDue: today.AddDays(20),
    received: 0m, demoActorId, now, createdOrders, updatedOrders, approved: true);

await UpsertOrderAsync(db, "SEED-PO-04", activePlan, lineByNo[4], supplierB,
    PurchaseOrderApprovalStatuses.Approved, PurchaseOrderPlacementStatuses.Placed,
    PurchaseOrderDeliveryStatuses.Partial, PurchaseOrderPaymentStatuses.Partial,
    paymentWarning: false, today.AddDays(-20), paymentDue: today.AddDays(10),
    received: 50m, demoActorId, now, createdOrders, updatedOrders, approved: true);

await UpsertOrderAsync(db, "SEED-PO-05", activePlan, lineByNo[5], supplierA,
    PurchaseOrderApprovalStatuses.Approved, PurchaseOrderPlacementStatuses.Placed,
    PurchaseOrderDeliveryStatuses.Complete, PurchaseOrderPaymentStatuses.Paid,
    paymentWarning: false, today.AddDays(-40), paymentDue: today.AddDays(-10),
    received: 300m, demoActorId, now, createdOrders, updatedOrders, approved: true);

await UpsertOrderAsync(db, "SEED-PO-06", activePlan, lineByNo[6], supplierB,
    PurchaseOrderApprovalStatuses.Approved, PurchaseOrderPlacementStatuses.Placed,
    PurchaseOrderDeliveryStatuses.Partial, PurchaseOrderPaymentStatuses.Overdue,
    paymentWarning: true, today.AddDays(-45), paymentDue: today.AddDays(-15),
    received: 25m, demoActorId, now, createdOrders, updatedOrders, approved: true);

// --- Closed plan: delivered lines ---
var closedPlan = await UpsertPlanAsync(
    db, PlanClosed, project.ProjectCode, PurchasePlanStatuses.Closed,
    "Seed demo — kế hoạch đã đóng", demoActorId, now, createdPlans, updatedPlans);
await UpsertPlanLinesAsync(db, closedPlan, new[]
{
    LineSpec(1, "Bulong M12", "Hộp", 30m, 85_000m, PurchaseLineStatuses.Delivered,
        orderedQty: 30, receivedQty: 30,
        purchaseUnit: 82_000m, purchaseSupplier: supplierA.SupplierName,
        orderedDate: today.AddDays(-60), expected: today.AddDays(-50), receipt: today.AddDays(-48)),
    LineSpec(2, "Keo silicone", "Tuýp", 100m, 25_000m, PurchaseLineStatuses.Delivered,
        orderedQty: 100, receivedQty: 100,
        purchaseUnit: 24_000m, purchaseSupplier: supplierB.SupplierName,
        orderedDate: today.AddDays(-55), expected: today.AddDays(-45), receipt: today.AddDays(-44)),
});

await RecalcPlanTotalsAsync(db, PlanDraft);
await RecalcPlanTotalsAsync(db, PlanActive);
await RecalcPlanTotalsAsync(db, PlanClosed);

await UpsertMarkerAsync(db, SeedMarkerKey, now);
await db.SaveChangesAsync();
await tx.CommitAsync();

Console.WriteLine();
Console.WriteLine("Đã seed purchasing demo:");
Console.WriteLine($"  Project : {project.ProjectCode} ({project.ProjectName})");
Console.WriteLine($"  Suppliers: {supplierA.SupplierCode}, {supplierB.SupplierCode}");
Console.WriteLine($"  Plans   : {PlanDraft} (draft), {PlanActive} (active), {PlanClosed} (closed)");
Console.WriteLine("  Orders  : SEED-PO-01..06 trên " + PlanActive);
if (createdPlans.Count > 0)
    Console.WriteLine("  + plans : " + string.Join(", ", createdPlans));
if (updatedPlans.Count > 0)
    Console.WriteLine("  ~ plans : " + string.Join(", ", updatedPlans));
if (createdOrders.Count > 0)
    Console.WriteLine("  + orders: " + string.Join(", ", createdOrders));
if (updatedOrders.Count > 0)
    Console.WriteLine("  ~ orders: " + string.Join(", ", updatedOrders));
Console.WriteLine();
Console.WriteLine("Re-run with force: dotnet run --project scripts/SeedPurchasingDemoData -- --force");
return 0;

// ───────────────────────── helpers ─────────────────────────

static LineDraft LineSpec(
    int lineNo, string product, string unit, decimal qty, decimal budgetUnit, string status,
    decimal orderedQty = 0, decimal receivedQty = 0, decimal defectQty = 0,
    decimal? proposalUnit = null, string? proposalSupplier = null,
    decimal? purchaseUnit = null, string? purchaseSupplier = null,
    DateOnly? orderedDate = null, DateOnly? expected = null, DateOnly? receipt = null) =>
    new(lineNo, product, unit, qty, budgetUnit, status, orderedQty, receivedQty, defectQty,
        proposalUnit, proposalSupplier, purchaseUnit, purchaseSupplier, orderedDate, expected, receipt);

async Task<Project> EnsureDemoProjectAsync(WarehouseDbContext db)
{
    var existing = await db.Projects
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(p => p.ProjectCode == ProjectCode);

    if (existing is not null)
    {
        existing.DeletedAt = null;
        existing.Status = ProjectStatus.Active;
        if (string.IsNullOrWhiteSpace(existing.ProjectName))
            existing.ProjectName = "Dự án demo Purchasing";
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return existing;
    }

    var project = new Project
    {
        ProjectCode = ProjectCode,
        ProjectName = "Dự án demo Purchasing",
        ProjectType = ProjectType.Project,
        PmId = Guid.Empty,
        PmName = "PM Demo Purchasing",
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
        MaterialBudget = 2_000_000_000m,
        Status = ProjectStatus.Active,
    };
    db.Projects.Add(project);
    await db.SaveChangesAsync();
    Console.WriteLine($"  Created project {ProjectCode}");
    return project;
}

async Task<(Supplier A, Supplier B)> EnsureDemoSuppliersAsync(WarehouseDbContext db)
{
    var a = await EnsureSupplierAsync(db, SupplierCodeA, "NCC Demo Purchasing A", 30);
    var b = await EnsureSupplierAsync(db, SupplierCodeB, "NCC Demo Purchasing B", 45);
    return (a, b);
}

async Task<Supplier> EnsureSupplierAsync(WarehouseDbContext db, string code, string name, int maxDebtDays)
{
    var existing = await db.Suppliers
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(s => s.SupplierCode == code);

    if (existing is not null)
    {
        existing.DeletedAt = null;
        existing.SupplierName = name;
        existing.MaxDebtDays = maxDebtDays;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return existing;
    }

    var supplier = new Supplier
    {
        SupplierCode = code,
        SupplierName = name,
        ContactPhone = "0900000000",
        MaxDebtDays = maxDebtDays,
        Rating = "A",
    };
    db.Suppliers.Add(supplier);
    await db.SaveChangesAsync();
    Console.WriteLine($"  Created supplier {code} (MaxDebtDays={maxDebtDays})");
    return supplier;
}

async Task<PurchasePlan> UpsertPlanAsync(
    WarehouseDbContext db, string code, string projectCode, string status, string note,
    Guid actorId, DateTime now, List<string> created, List<string> updated)
{
    var plan = await db.PurchasePlans
        .IgnoreQueryFilters()
        .Include(p => p.Lines)
        .FirstOrDefaultAsync(p => p.Code == code);

    if (plan is null)
    {
        plan = new PurchasePlan
        {
            Code = code,
            ProjectCode = projectCode,
            Status = status,
            Note = note,
            CreatedById = actorId,
            CreatedByName = "Seed Purchasing Demo",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PurchasePlans.Add(plan);
        created.Add(code);
    }
    else
    {
        plan.DeletedAt = null;
        plan.ProjectCode = projectCode;
        plan.Status = status;
        plan.Note = note;
        plan.UpdatedAt = now;
        updated.Add(code);
    }

    return plan;
}

async Task<List<PurchasePlanLine>> UpsertPlanLinesAsync(
    WarehouseDbContext db, PurchasePlan plan, IReadOnlyList<LineDraft> specs)
{
    var result = new List<PurchasePlanLine>();
    foreach (var s in specs)
    {
        var line = plan.Lines.FirstOrDefault(l => l.LineNo == s.LineNo);
        if (line is null)
        {
            line = new PurchasePlanLine { PlanCode = plan.Code, LineNo = s.LineNo };
            plan.Lines.Add(line);
            db.PurchasePlanLines.Add(line);
        }

        line.ProductName = s.ProductName;
        line.Category = "Seed";
        line.SystemName = "SEED";
        line.Spec = "Demo seed line";
        line.Unit = s.Unit;
        line.Quantity = s.Quantity;
        line.PurchaseStatus = s.Status;
        line.BudgetUnitPrice = s.BudgetUnit;
        line.BudgetAmount = Math.Round(s.Quantity * s.BudgetUnit, 2);
        line.OrderedQty = s.OrderedQty;
        line.ReceivedQty = s.ReceivedQty;
        line.DefectQty = s.DefectQty;
        line.OrderedDate = s.OrderedDate;
        line.ExpectedDeliveryDate = s.Expected;
        line.ActualReceiptDate = s.Receipt;

        if (s.ProposalUnit is decimal pu)
        {
            line.ProposalUnitPrice = pu;
            line.ProposalAmount = Math.Round(s.Quantity * pu, 2);
            line.ProposalSupplierName = s.ProposalSupplier;
            line.ProposalStatus = "đề xuất";
        }

        if (s.PurchaseUnit is decimal buy)
        {
            line.PurchaseUnitPrice = buy;
            line.PurchaseAmount = Math.Round((s.OrderedQty > 0 ? s.OrderedQty : s.Quantity) * buy, 2);
            line.PurchaseSupplierName = s.PurchaseSupplier;
            line.SelectedUnitPrice = buy;
            line.SelectedAmount = line.PurchaseAmount;
            line.SelectedSupplierName = s.PurchaseSupplier;
        }
        else if (s.ProposalUnit is decimal prop)
        {
            line.SelectedUnitPrice = prop;
            line.SelectedAmount = Math.Round(s.Quantity * prop, 2);
            line.SelectedSupplierName = s.ProposalSupplier;
        }

        result.Add(line);
    }

    await db.SaveChangesAsync();
    return result;
}

async Task UpsertOrderAsync(
    WarehouseDbContext db, string code, PurchasePlan plan, PurchasePlanLine line, Supplier supplier,
    string approval, string placement, string delivery, string payment,
    bool paymentWarning, DateOnly orderDate, DateOnly? paymentDue, decimal received,
    Guid actorId, DateTime now, List<string> created, List<string> updated, bool approved = false)
{
    var qty = line.Quantity;
    var price = line.PurchaseUnitPrice ?? line.SelectedUnitPrice ?? line.BudgetUnitPrice ?? 0m;
    var total = Math.Round(qty * price, 2);

    var order = await db.PurchaseOrders
        .IgnoreQueryFilters()
        .Include(o => o.Items)
        .FirstOrDefaultAsync(o => o.Code == code);

    if (order is null)
    {
        order = new PurchaseOrder
        {
            Code = code,
            CreatedById = actorId,
            CreatedByName = "Seed Purchasing Demo",
            CreatedAt = now,
        };
        db.PurchaseOrders.Add(order);
        created.Add(code);
    }
    else
    {
        order.DeletedAt = null;
        updated.Add(code);
    }

    order.ProjectCode = plan.ProjectCode;
    order.PlanCode = plan.Code;
    order.SupplierId = supplier.Id;
    order.OrderDate = orderDate;
    order.ApprovalStatus = approval;
    order.OrderPlacementStatus = placement;
    order.DeliveryStatus = delivery;
    order.PaymentStatus = payment;
    order.PaymentWarning = paymentWarning;
    order.Note = $"Seed PO — {approval}/{placement}/{delivery}/{payment}";
    order.TotalAmount = total;
    order.SupplierPaymentTermsDays = supplier.MaxDebtDays;
    order.SupplierNameSnapshot = supplier.SupplierName;
    order.PaymentDueDate = paymentDue;
    order.UpdatedAt = now;

    if (approved)
    {
        order.ApprovedById = actorId;
        order.ApprovedByName = "Seed Approver";
        order.ApprovedAt = now.AddDays(-1);
    }
    else
    {
        order.ApprovedById = null;
        order.ApprovedByName = null;
        order.ApprovedAt = null;
    }

    var item = order.Items.FirstOrDefault();
    if (item is null)
    {
        item = new PurchaseOrderItem { PoCode = code };
        order.Items.Add(item);
        db.PurchaseOrderItems.Add(item);
    }

    item.PlanLineId = line.Id;
    item.Sku = $"SEED-SKU-{line.LineNo:D2}";
    item.Name = line.ProductName;
    item.Spec = line.Spec;
    item.EstPrice = line.BudgetUnitPrice ?? price;
    item.PoPrice = price;
    item.Ordered = qty;
    item.Received = received;
    item.Unit = line.Unit;
    item.Note = "Seed item";
    item.ExpectedDeliveryDate = line.ExpectedDeliveryDate ?? orderDate.AddDays(14);

    await db.SaveChangesAsync();
}

async Task RecalcPlanTotalsAsync(WarehouseDbContext db, string planCode)
{
    var plan = await db.PurchasePlans
        .Include(p => p.Lines)
        .Include(p => p.PurchaseOrders)
        .FirstAsync(p => p.Code == planCode);

    plan.BudgetTotal = Math.Round(plan.Lines.Sum(l => l.BudgetAmount ?? 0m), 2);
    plan.ProposalTotal = Math.Round(plan.Lines.Sum(l => l.ProposalAmount ?? 0m), 2);
    plan.PurchaseTotal = Math.Round(plan.Lines.Sum(l =>
        l.SelectedAmount ?? l.PurchaseAmount ?? 0m), 2);

    plan.PaidTotal = Math.Round(plan.PurchaseOrders
        .Where(o => o.DeletedAt == null)
        .Sum(o => o.PaymentStatus switch
        {
            PurchaseOrderPaymentStatuses.Paid => o.TotalAmount,
            PurchaseOrderPaymentStatuses.Partial => Math.Round(o.TotalAmount / 2m, 2),
            _ => 0m,
        }), 2);

    plan.UpdatedAt = DateTime.UtcNow;
}

async Task UpsertMarkerAsync(WarehouseDbContext db, string key, DateTime now)
{
    var marker = await db.WmsSettings.FirstOrDefaultAsync(s => s.Key == key);
    if (marker is null)
    {
        db.WmsSettings.Add(new WmsSetting { Key = key, Value = now.ToString("O") });
    }
    else
    {
        marker.Value = now.ToString("O");
        marker.UpdatedAt = now;
        marker.DeletedAt = null;
    }
}

internal sealed record LineDraft(
    int LineNo,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal BudgetUnit,
    string Status,
    decimal OrderedQty,
    decimal ReceivedQty,
    decimal DefectQty,
    decimal? ProposalUnit,
    string? ProposalSupplier,
    decimal? PurchaseUnit,
    string? PurchaseSupplier,
    DateOnly? OrderedDate,
    DateOnly? Expected,
    DateOnly? Receipt);
