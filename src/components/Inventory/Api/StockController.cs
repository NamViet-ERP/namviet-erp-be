using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("matrix")]
    [RequirePermission("wms.stock.view:all")]
    public async Task<ActionResult<List<StockMatrixRowDto>>> Matrix(CancellationToken ct)
    {
        var canViewPrices = PriceVisibility.CanViewPrices(User);
        var generalWarehouseId = await inventory.GetGeneralWarehouseIdAsync(ct);

        var projectInventories = await db.ProjectInventories.AsNoTracking().ToListAsync(ct);
        var warehouseInventories = await db.WarehouseInventories
            .AsNoTracking()
            .Where(i => i.WarehouseId == generalWarehouseId)
            .ToListAsync(ct);

        // HL trên tồn kho: mọi phiếu chưa từ chối (draft/pending/approved).
        // Join tường minh theo TicketCode — tránh LINQ navigation filter làm trống kết quả.
        var defectedRows = await (
            from i in db.PurchaseDefectItems.AsNoTracking()
            join d in db.PurchaseDefects.AsNoTracking() on i.TicketCode equals d.TicketCode
            where d.Status != PurchaseDefectStatuses.Rejected
                  && i.MaterialCode != null
                  && i.MaterialCode != ""
            group i by i.MaterialCode into g
            select new { MaterialCode = g.Key!, Qty = g.Sum(x => x.Quantity) }
        ).ToListAsync(ct);
        var defectedByMaterial = defectedRows.ToDictionary(
            x => x.MaterialCode,
            x => x.Qty,
            StringComparer.OrdinalIgnoreCase);

        var codesWithStock = projectInventories
            .Where(pi =>
                pi.ActualImportQty - pi.ActualExportQty != 0 ||
                pi.PlannedImportQty != 0 ||
                pi.PlannedExportQty != 0 ||
                pi.ReservedQty != 0)
            .Select(pi => pi.MaterialCode)
            .Concat(warehouseInventories.Where(wi => wi.Qty != 0).Select(wi => wi.MaterialCode))
            .Concat(defectedByMaterial.Keys)
            .ToHashSet(StringComparer.Ordinal);

        var materials = await db.Materials
            .AsNoTracking()
            .Where(m => m.DeletedAt == null && (m.IsActive || codesWithStock.Contains(m.MaterialCode)))
            .OrderBy(m => m.MaterialCode)
            .ToListAsync(ct);

        var projects = await db.Projects
            .AsNoTracking()
            .Where(p => p.DeletedAt == null && p.ProjectType == ProjectType.Project)
            .OrderBy(p => p.ProjectName)
            .ToListAsync(ct);

        var holdingReservations = await db.InventoryReservations
            .AsNoTracking()
            .Where(r => r.WarehouseId == generalWarehouseId && r.Status == ReservationStatus.Holding)
            .GroupBy(r => r.MaterialCode)
            .Select(g => new { MaterialCode = g.Key, Qty = g.Sum(x => x.ReservedQty) })
            .ToDictionaryAsync(x => x.MaterialCode, x => x.Qty, ct);

        var pendingHlHolds = await inventory.ListPendingStockDefectHoldsAsync(ct);
        var pendingHlLookup = new Dictionary<(string Project, string Material), decimal>();
        foreach (var h in pendingHlHolds)
        {
            var key = MatrixKey(h.ProjectCode, h.MaterialCode);
            pendingHlLookup[key] = pendingHlLookup.GetValueOrDefault(key) + h.Quantity;
        }

        decimal PendingHlFor(string projectCode, string materialCode) =>
            pendingHlLookup.GetValueOrDefault(MatrixKey(projectCode, materialCode));

        var projectInventoryLookup = projectInventories.ToDictionary(
            pi => MatrixKey(pi.ProjectCode, pi.MaterialCode));

        var warehouseInventoryLookup = warehouseInventories.ToDictionary(
            wi => wi.MaterialCode,
            wi => wi.Qty,
            StringComparer.OrdinalIgnoreCase);

        // WAC (Weighted Average Cost) — IAS 2: phiếu nhập đã duyệt trong 12 tháng theo ngày duyệt
        var wacFrom = DateTime.UtcNow.AddYears(-1);
        var wacByMaterial = await db.WarehouseTransactionItems
            .AsNoTracking()
            .Where(i =>
                i.Transaction!.TransactionType == TransactionType.Import
                && i.Transaction.ApprovalStatus == ApprovalStatus.Approved
                && i.Transaction.ApprovedAt != null
                && i.Transaction.ApprovedAt >= wacFrom
                && i.UnitPrice > 0
                && i.Quantity > 0)
            .GroupBy(i => i.MaterialCode)
            .Select(g => new
            {
                MaterialCode = g.Key,
                TotalQty = g.Sum(x => x.Quantity),
                TotalCost = g.Sum(x => x.Quantity * x.UnitPrice),
            })
            .ToDictionaryAsync(
                x => x.MaterialCode,
                x => x.TotalQty > 0 ? x.TotalCost / x.TotalQty : 0m,
                ct);

        var rows = new List<StockMatrixRowDto>();
        foreach (var material in materials)
        {
            var generalActual = warehouseInventoryLookup.GetValueOrDefault(material.MaterialCode);
            var generalReserved = holdingReservations.GetValueOrDefault(material.MaterialCode);
            var generalAvailable = generalActual - generalReserved;

            var projectColumns = projects.Select(p =>
            {
                projectInventoryLookup.TryGetValue(MatrixKey(p.ProjectCode, material.MaterialCode), out var pi);
                return new StockMatrixProjectColumnDto(
                    p.ProjectCode,
                    p.ClientProjectId ?? p.ProjectCode,
                    p.ProjectName,
                    pi?.ActualImportQty - pi?.ActualExportQty ?? 0m,
                    pi?.PlannedImportQty ?? 0m,
                    pi?.PlannedExportQty ?? 0m,
                    PendingHlFor(p.ProjectCode, material.MaterialCode));
            }).ToList();

            rows.Add(new StockMatrixRowDto(
                material.MaterialCode,
                material.MaterialName,
                material.Unit,
                material.IsActive,
                generalActual,
                generalReserved,
                generalAvailable,
                PriceVisibility.Mask(canViewPrices, wacByMaterial.GetValueOrDefault(material.MaterialCode)),
                defectedByMaterial.GetValueOrDefault(material.MaterialCode),
                projectColumns));
        }

        return Ok(rows);
    }

    [HttpGet("admin/opening-balance/template")]
    [RequirePermission("wms.stock.view:all")]
    public IActionResult DownloadOpeningBalanceTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("TonKhoDauKy");

        string[] headers =
        [
            "Mã vật tư",
            "Tên vật tư",
            "Đơn vị tính",
            "Số lượng tồn",
            "Đơn giá vốn",
            "Ghi chú"
        ];

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
            cell.Style.Font.FontColor = XLColor.White;
        }

        ws.Cell(2, 1).Value = "VT-THEP-001";
        ws.Cell(2, 2).Value = "Ống thép đúc DN100 SCH40";
        ws.Cell(2, 3).Value = "Cây";
        ws.Cell(2, 4).Value = 50;
        ws.Cell(2, 5).Value = 850000;
        ws.Cell(2, 6).Value = "Tồn kho đầu kỳ kiểm kê 2026";

        ws.Cell(3, 1).Value = "VT-VAN-002";
        ws.Cell(3, 2).Value = "Van cổng ty chìm gang DN100 PN16";
        ws.Cell(3, 3).Value = "Cái";
        ws.Cell(3, 4).Value = 20;
        ws.Cell(3, 5).Value = 1250000;
        ws.Cell(3, 6).Value = "Tồn kho đầu kỳ kiểm kê 2026";

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Mau_Nhap_Ton_Kho_Dau_Ky.xlsx");
    }

    [HttpPost("admin/opening-balance/preview")]
    public async Task<ActionResult<OpeningStockPreviewResponseDto>> PreviewOpeningBalance(
        IFormFile file,
        [FromQuery] string? projectCode,
        CancellationToken ct)
    {
        if (!IsAdminUser())
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel tồn kho đầu kỳ.");

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return BadRequest("File Excel không có sheet nào.");

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var projExists = await db.Projects.AsNoTracking().AnyAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
            if (!projExists)
                return BadRequest($"Không tìm thấy dự án với mã '{projectCode}'.");
        }

        var materials = await db.Materials.AsNoTracking()
            .Where(m => m.DeletedAt == null)
            .ToDictionaryAsync(m => m.MaterialCode.ToUpperInvariant(), ct);

        var previewItems = new List<OpeningStockItemPreviewDto>();
        var rowsUsed = ws.RowsUsed().Skip(1);
        var rowIdx = 1;

        foreach (var row in rowsUsed)
        {
            rowIdx++;
            var code = row.Cell(1).GetString().Trim();
            var name = row.Cell(2).GetString().Trim();
            var unit = row.Cell(3).GetString().Trim();
            var qtyStr = row.Cell(4).GetString().Trim();
            var priceStr = row.Cell(5).GetString().Trim();
            var note = row.Cell(6).GetString().Trim();

            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(qtyStr))
                continue;

            decimal.TryParse(qtyStr, out var qty);
            decimal.TryParse(priceStr, out var price);

            var isValid = true;
            var errorList = new List<string>();

            if (string.IsNullOrWhiteSpace(code))
            {
                isValid = false;
                errorList.Add("Mã vật tư không được để trống");
            }
            else if (!materials.TryGetValue(code.ToUpperInvariant(), out var mat))
            {
                isValid = false;
                errorList.Add($"Mã vật tư '{code}' chưa tồn tại trong danh mục hệ thống");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(name)) name = mat.MaterialName;
                if (string.IsNullOrWhiteSpace(unit)) unit = mat.Unit;
            }

            if (qty <= 0)
            {
                isValid = false;
                errorList.Add("Số lượng tồn phải lớn hơn 0");
            }

            if (price < 0)
            {
                isValid = false;
                errorList.Add("Đơn giá không được âm");
            }

            previewItems.Add(new OpeningStockItemPreviewDto(
                MaterialCode: code,
                MaterialName: name,
                Unit: unit,
                Qty: qty,
                UnitPrice: price,
                TotalAmount: Math.Round(qty * price, 2),
                Note: string.IsNullOrWhiteSpace(note) ? null : note,
                IsValid: isValid,
                Error: errorList.Count > 0 ? string.Join("; ", errorList) : null));
        }

        var total = previewItems.Count;
        var valid = previewItems.Count(i => i.IsValid);
        var errorCount = total - valid;
        var totalQty = previewItems.Where(i => i.IsValid).Sum(i => i.Qty);
        var totalAmount = previewItems.Where(i => i.IsValid).Sum(i => i.TotalAmount);

        return Ok(new OpeningStockPreviewResponseDto(total, valid, errorCount, totalQty, totalAmount, previewItems));
    }

    [HttpPost("admin/opening-balance/import-excel")]
    public async Task<ActionResult<OpeningStockImportResultDto>> ImportOpeningBalance(
        IFormFile file,
        [FromQuery] string? projectCode,
        CancellationToken ct)
    {
        if (!IsAdminUser())
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel tồn kho đầu kỳ.");

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return BadRequest("File Excel không có sheet nào.");

        Project? project = null;
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
            if (project is null)
                return BadRequest($"Không tìm thấy dự án với mã '{projectCode}'.");
        }

        var materials = await db.Materials
            .Where(m => m.DeletedAt == null)
            .ToDictionaryAsync(m => m.MaterialCode.ToUpperInvariant(), ct);

        var parsedItems = new List<(Material Mat, decimal Qty, decimal Price, string? Note)>();
        var errors = new List<string>();
        var rowsUsed = ws.RowsUsed().Skip(1);
        var rowIdx = 1;

        foreach (var row in rowsUsed)
        {
            rowIdx++;
            var code = row.Cell(1).GetString().Trim();
            var name = row.Cell(2).GetString().Trim();
            var qtyStr = row.Cell(4).GetString().Trim();
            var priceStr = row.Cell(5).GetString().Trim();
            var note = row.Cell(6).GetString().Trim();

            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(qtyStr))
                continue;

            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add($"Dòng {rowIdx}: Mã vật tư không được để trống.");
                continue;
            }

            if (!materials.TryGetValue(code.ToUpperInvariant(), out var mat))
            {
                errors.Add($"Dòng {rowIdx}: Mã vật tư '{code}' chưa tồn tại trong danh mục vật tư.");
                continue;
            }

            if (!decimal.TryParse(qtyStr, out var qty) || qty <= 0)
            {
                errors.Add($"Dòng {rowIdx}: Số lượng tồn '{qtyStr}' không hợp lệ (phải > 0).");
                continue;
            }

            decimal.TryParse(priceStr, out var price);
            if (price < 0) price = 0;

            parsedItems.Add((mat, qty, price, note));
        }

        if (errors.Count > 0)
            return BadRequest(new { message = "File Excel có lỗi dữ liệu, vui lòng kiểm tra lại.", errors });

        if (parsedItems.Count == 0)
            return BadRequest("Không tìm thấy dòng dữ liệu nào hợp lệ trong file Excel.");

        var ticketCode = await TicketCodeAllocator.NextAsync(db, TransactionType.Import, ct);
        var userName = !string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Name : "Admin";
        var generalWarehouseId = await inventory.GetGeneralWarehouseIdAsync(ct);

        var transaction = new WarehouseTransaction
        {
            TicketCode = ticketCode,
            TransactionType = TransactionType.Import,
            ProjectCode = project?.ProjectCode,
            InvoiceNumber = "OPENING-STOCK",
            CreatedById = currentUser.IsAuthenticated ? currentUser.Id : Guid.Empty,
            CreatedByName = userName,
            ApprovalStatus = ApprovalStatus.Approved,
            WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved,
            ApprovedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            ApprovedByName = userName,
            ApprovedAt = DateTime.UtcNow,
            Notes = $"Nạp số dư tồn kho đầu kỳ{(project != null ? $" cho dự án {project.ProjectName}" : " kho tổng")} từ file Excel",
            WarehouseId = project == null ? generalWarehouseId : null,
            BeneficiaryProjectCode = project?.ProjectCode,
            WarehouseKeeper = userName,
            CreatedAt = DateTime.UtcNow,
        };

        db.WarehouseTransactions.Add(transaction);

        foreach (var item in parsedItems)
        {
            var txItem = new WarehouseTransactionItem
            {
                TicketCode = ticketCode,
                MaterialCode = item.Mat.MaterialCode,
                MaterialNameSnapshot = item.Mat.MaterialName,
                UnitSnapshot = item.Mat.Unit,
                Quantity = item.Qty,
                PlannedQuantity = item.Qty,
                AcceptedQty = item.Qty,
                UnitPrice = item.Price,
                Notes = item.Note,
            };
            db.WarehouseTransactionItems.Add(txItem);

            if (project == null)
            {
                // Kho tổng
                var inv = await db.WarehouseInventories.FirstOrDefaultAsync(
                    wi => wi.WarehouseId == generalWarehouseId && wi.MaterialCode == item.Mat.MaterialCode,
                    ct);

                if (inv is null)
                {
                    db.WarehouseInventories.Add(new WarehouseInventory
                    {
                        WarehouseId = generalWarehouseId,
                        MaterialCode = item.Mat.MaterialCode,
                        Qty = item.Qty,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    inv.Qty += item.Qty;
                    inv.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Kho dự án
                var pi = await db.ProjectInventories.FirstOrDefaultAsync(
                    p => p.ProjectCode == project.ProjectCode && p.MaterialCode == item.Mat.MaterialCode,
                    ct);

                if (pi is null)
                {
                    db.ProjectInventories.Add(new ProjectInventory
                    {
                        ProjectCode = project.ProjectCode,
                        MaterialCode = item.Mat.MaterialCode,
                        ActualImportQty = item.Qty,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    pi.ActualImportQty += item.Qty;
                    pi.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var totalQty = parsedItems.Sum(i => i.Qty);
        var totalAmount = parsedItems.Sum(i => i.Qty * i.Price);

        return Ok(new OpeningStockImportResultDto(
            ticketCode,
            parsedItems.Count,
            totalQty,
            totalAmount,
            $"Nạp thành công {parsedItems.Count} mục vật tư vào {(project != null ? $"kho dự án {project.ProjectCode}" : "kho tổng")}. Mã phiếu: {ticketCode}"));
    }

    private bool IsAdminUser() =>
        currentUser.IsInRole("Admin")
        || currentUser.Roles.Any(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase))
        || currentUser.HasPermission("admin.users.view:all")
        || User.IsInRole("Admin")
        || User.HasClaim("role", "Admin");

    private static (string Project, string Material) MatrixKey(string projectCode, string materialCode) =>
        (projectCode.ToUpperInvariant(), materialCode.ToUpperInvariant());
}
