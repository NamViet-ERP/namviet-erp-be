using ClosedXML.Excel;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchasePlanImportService
{
    public static readonly string[] ExpenseKeywords =
    [
        "tiếp khách", "tiep khach",
        "di chuyển", "di chuyen",
        "đi lại", "di lai",
        "nhân công", "nhan cong",
        "quản lý", "quan ly",
        "lương", "luong",
        "phát sinh", "phat sinh",
        "công tác", "cong tac",
        "chi phí", "chi phi",
        "bảo hành", "bao hanh",
        "thuế", "thue tncn", "thue vat", "thue gtgt", "tien thue"
    ];

    public static readonly string[] ServiceKeywords =
    [
        "vận chuyển", "van chuyen",
        "kiểm định", "kiem dinh",
        "đo khí thải", "do khi thai",
        "bể dập bụi", "be dap bui",
        "gia công", "gia cong",
        "lắp đặt", "lap dat",
        "chế tạo", "che tao",
        "thầu phụ", "thau phu",
        "subcontract",
        "dịch vụ", "dich vu",
        "bảo dưỡng", "bao duong",
        "bảo trì", "bao tri",
        "sửa chữa", "sua chua",
        "thuê ngoài", "thue ngoai"
    ];

    public static readonly string[] ServiceUnits =
    [
        "hệ", "he",
        "gói", "goi",
        "chuyến", "chuyen",
        "lần", "lan"
    ];

    public static bool IsServiceOrExpenseLine(
        string? productName,
        string? category,
        string? systemName,
        string? unit)
    {
        var textCombined = $"{productName} {category} {systemName}".ToLowerInvariant();
        var normCombined = Normalize(textCombined);
        var normUnit = Normalize(unit ?? string.Empty).Trim();

        foreach (var kw in ExpenseKeywords)
        {
            if (kw == "thuế")
            {
                if (textCombined.Contains("thuế", StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            if (kw == "lương" || kw == "luong")
            {
                if (textCombined.Contains("lương", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (normCombined.Contains("luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("luu luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("trong luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("khoi luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("so luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("chat luong", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                continue;
            }

            if (textCombined.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                normCombined.Contains(Normalize(kw), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var kw in ServiceKeywords)
        {
            if (textCombined.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                normCombined.Contains(Normalize(kw), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var u in ServiceUnits)
        {
            if (string.Equals(normUnit, Normalize(u), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string ClassifyLineType(
        string? materialCode,
        string? productName,
        string? category,
        string? systemName,
        string? unit,
        bool hasPriceOrMoney = false,
        decimal? qty = null)
    {
        return ClassifyLineType(materialCode, productName, category, systemName, unit, hasPriceOrMoney, qty, null);
    }

    public static string ClassifyLineType(
        string? materialCode,
        string? productName,
        string? category,
        string? systemName,
        string? unit,
        bool hasPriceOrMoney,
        decimal? qty,
        IReadOnlyDictionary<string, string>? activeServiceItemTypes)
    {
        if (!string.IsNullOrWhiteSpace(materialCode))
        {
            if (activeServiceItemTypes != null && activeServiceItemTypes.TryGetValue(materialCode.Trim(), out var stype))
            {
                return stype == "expense" ? PurchasePlanLineTypes.Expense : PurchasePlanLineTypes.Service;
            }
            return PurchasePlanLineTypes.Material;
        }

        var textCombined = $"{productName} {category} {systemName}".ToLowerInvariant();
        var normCombined = Normalize(textCombined);
        var normUnit = Normalize(unit ?? string.Empty).Trim();

        foreach (var kw in ExpenseKeywords)
        {
            if (kw == "thuế")
            {
                if (textCombined.Contains("thuế", StringComparison.OrdinalIgnoreCase))
                    return PurchasePlanLineTypes.Expense;
                continue;
            }

            if (kw == "lương" || kw == "luong")
            {
                if (textCombined.Contains("lương", StringComparison.OrdinalIgnoreCase))
                    return PurchasePlanLineTypes.Expense;

                if (normCombined.Contains("luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("luu luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("trong luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("khoi luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("so luong", StringComparison.OrdinalIgnoreCase)
                    && !normCombined.Contains("chat luong", StringComparison.OrdinalIgnoreCase))
                {
                    return PurchasePlanLineTypes.Expense;
                }
                continue;
            }

            if (textCombined.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                normCombined.Contains(Normalize(kw), StringComparison.OrdinalIgnoreCase))
            {
                return PurchasePlanLineTypes.Expense;
            }
        }

        foreach (var kw in ServiceKeywords)
        {
            if (textCombined.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                normCombined.Contains(Normalize(kw), StringComparison.OrdinalIgnoreCase))
            {
                return PurchasePlanLineTypes.Service;
            }
        }

        foreach (var u in ServiceUnits)
        {
            if (string.Equals(normUnit, Normalize(u), StringComparison.OrdinalIgnoreCase))
            {
                return PurchasePlanLineTypes.Service;
            }
        }

        return PurchasePlanLineTypes.Material;
    }

    private static bool IsHeaderOrSeparatorRow(
        IXLWorksheet sheet,
        int row,
        Dictionary<string, int> fieldMap,
        List<QuoteGroupColumns> quoteGroups)
    {
        string? Get(string field) =>
            fieldMap.TryGetValue(field, out var col) ? CellText(sheet.Cell(row, col)) : null;

        var matCode = Get("MaterialCode");
        if (!string.IsNullOrWhiteSpace(matCode))
            return false;

        var prodName = Get("ProductName");
        if (string.IsNullOrWhiteSpace(prodName))
            return true;

        var stt = Get("LineNo");
        if (string.IsNullOrWhiteSpace(stt))
            stt = CellText(sheet.Cell(row, 1));

        if (!string.IsNullOrWhiteSpace(stt) && RomanNumeralRegex.IsMatch(stt.Trim()))
            return true;

        var spec = Get("Spec");
        var unit = Get("Unit");
        var qty = ParseDecimal(Get("Quantity"));

        var hasNoSpec = string.IsNullOrWhiteSpace(spec);
        var hasNoUnit = string.IsNullOrWhiteSpace(unit);
        var hasNoQty = qty is null or <= 0;

        var hasMoney = ParseDecimal(Get("BudgetUnitPrice")) != null
            || ParseDecimal(Get("BudgetAmount")) != null
            || ParseDecimal(Get("ProposalUnitPrice")) != null
            || ParseDecimal(Get("ProposalAmount")) != null
            || ParseDecimal(Get("PurchaseUnitPrice")) != null
            || ParseDecimal(Get("PurchaseAmount")) != null
            || ParseDecimal(Get("SelectedUnitPrice")) != null
            || ParseDecimal(Get("SelectedAmount")) != null;

        if (!hasMoney)
        {
            foreach (var g in quoteGroups)
            {
                if (g.UnitPriceCol is int uc && ParseDecimal(CellText(sheet.Cell(row, uc))) != null) { hasMoney = true; break; }
                if (g.AmountCol is int ac && ParseDecimal(CellText(sheet.Cell(row, ac))) != null) { hasMoney = true; break; }
            }
        }

        // Dòng Subtotal / Header không có STT La Mã nhưng:
        // Không có MaterialCode VÀ không có Spec VÀ (không có Unit HOẶC không có Quantity / Quantity <= 0)
        if (hasNoSpec && (hasNoUnit || hasNoQty))
        {
            var category = Get("Category");
            var systemName = Get("SystemName");
            var isRecognizedServiceOrExpense = IsServiceOrExpenseLine(prodName, category, systemName, unit);

            // Dịch vụ / chi phí thực tế (ví dụ: Bể dập bụi, Vận chuyển, Kiểm định, Tiếp khách):
            // Có ĐVT dịch vụ chuẩn HOẶC từ khóa dịch vụ/chi phí, VÀ có tiền, VÀ không thiếu cả Unit lẫn Qty
            if (isRecognizedServiceOrExpense && hasMoney && (!hasNoUnit || !hasNoQty))
                return false;

            return true;
        }

        if (!hasMoney && hasNoQty)
            return true;

        return false;
    }

    private static PurchasePlanImportLinePreviewDto ParseRow(
        IXLWorksheet sheet,
        int row,
        int lineNo,
        Dictionary<string, int> fieldMap,
        List<QuoteGroupColumns> quoteGroups,
        List<PurchasePlanImportErrorDto> errors,
        IReadOnlySet<string>? activeMaterialCodes = null,
        IReadOnlyDictionary<string, string>? activeServiceItemTypes = null)
    {
        string? Get(string field) =>
            fieldMap.TryGetValue(field, out var col) ? CellText(sheet.Cell(row, col)) : null;

        var materialCode = NullIfEmpty(Get("MaterialCode")?.Trim());
        var productName = Get("ProductName")?.Trim() ?? string.Empty;
        var unit = Get("Unit")?.Trim() ?? string.Empty;
        var qtyRaw = Get("Quantity");
        var qty = ParseDecimal(qtyRaw);

        var budgetUnitPrice = ParseDecimal(Get("BudgetUnitPrice"));
        var budgetAmount = ParseDecimal(Get("BudgetAmount"));
        var proposalKg = ParseDecimal(Get("ProposalKgPerUnit"));
        var proposalUnitPrice = ParseDecimal(Get("ProposalUnitPrice"));
        var proposalAmount = ParseDecimal(Get("ProposalAmount"));
        var purchaseKg = ParseDecimal(Get("PurchaseKgPerUnit"));
        var purchaseUnitPrice = ParseDecimal(Get("PurchaseUnitPrice"));
        var purchaseAmount = ParseDecimal(Get("PurchaseAmount"));
        var selectedUnitPrice = ParseDecimal(Get("SelectedUnitPrice"));
        var selectedAmount = ParseDecimal(Get("SelectedAmount"));

        var quotes = new List<PurchasePlanImportQuotePreviewDto>();
        foreach (var g in quoteGroups)
        {
            var supplier = g.SupplierNameCol is int sc ? CellText(sheet.Cell(row, sc)) : null;
            var qUnitPrice = g.UnitPriceCol is int uc ? ParseDecimal(CellText(sheet.Cell(row, uc))) : null;
            var qAmount = g.AmountCol is int ac ? ParseDecimal(CellText(sheet.Cell(row, ac))) : null;
            var kg = g.KgPerUnitCol is int kc ? ParseDecimal(CellText(sheet.Cell(row, kc))) : null;
            var note = g.NoteCol is int nc ? CellText(sheet.Cell(row, nc)) : null;
            var status = g.StatusCol is int stc ? CellText(sheet.Cell(row, stc)) : null;

            if (string.IsNullOrWhiteSpace(supplier) && qUnitPrice is null && qAmount is null && kg is null)
                continue;

            quotes.Add(new PurchasePlanImportQuotePreviewDto(
                g.SortOrder, kg, qUnitPrice, qAmount,
                string.IsNullOrWhiteSpace(supplier) ? null : supplier,
                string.IsNullOrWhiteSpace(note) ? null : note,
                string.IsNullOrWhiteSpace(status) ? null : status));
        }

        var hasPriceOrMoney = budgetUnitPrice != null
            || budgetAmount != null
            || proposalUnitPrice != null
            || proposalAmount != null
            || purchaseUnitPrice != null
            || purchaseAmount != null
            || selectedUnitPrice != null
            || selectedAmount != null
            || quotes.Any(q => q.UnitPrice != null || q.Amount != null);

        var category = NullIfEmpty(Get("Category"));
        var systemName = NullIfEmpty(Get("SystemName"));

        var lineType = ClassifyLineType(materialCode, productName, category, systemName, unit, hasPriceOrMoney, qty, activeServiceItemTypes);
        var isValid = true;

        if (lineType == PurchasePlanLineTypes.Material)
        {
            if (string.IsNullOrWhiteSpace(materialCode))
            {
                errors.Add(new PurchasePlanImportErrorDto(row, "MaterialCode", "Thiếu mã vật tư."));
                isValid = false;
            }
            else if (activeMaterialCodes != null && !activeMaterialCodes.Contains(materialCode.Trim()))
            {
                if (activeServiceItemTypes != null && activeServiceItemTypes.TryGetValue(materialCode.Trim(), out var stype))
                {
                    lineType = stype == "expense" ? PurchasePlanLineTypes.Expense : PurchasePlanLineTypes.Service;
                }
                else
                {
                    errors.Add(new PurchasePlanImportErrorDto(
                        row,
                        "MaterialCode",
                        $"Dòng Excel {row}: Mã vật tư '{materialCode}' chưa có trong danh mục hệ thống."));
                    isValid = false;
                }
            }

            if (qty is null or <= 0)
            {
                errors.Add(new PurchasePlanImportErrorDto(row, "Quantity", "Số lượng phải > 0."));
                isValid = false;
                qty ??= 0;
            }
        }
        else
        {
            // Service & Expense: Không bắt buộc MaterialCode, cho phép rỗng/null, không thêm lỗi thiếu mã vật tư!
            if (qty is null or <= 0)
            {
                if (hasPriceOrMoney)
                {
                    qty = 1m;
                }
                else
                {
                    errors.Add(new PurchasePlanImportErrorDto(row, "Quantity", "Số lượng phải > 0."));
                    isValid = false;
                    qty ??= 0;
                }
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                errors.Add(new PurchasePlanImportErrorDto(row, "ProductName", "Thiếu tên dịch vụ / chi phí."));
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(unit))
            {
                unit = lineType == PurchasePlanLineTypes.Expense ? "Gói" : "Hệ";
            }

            if (!hasPriceOrMoney && (qty is null or <= 0))
            {
                isValid = false;
            }
        }

        // Tự động tính đơn giá hoặc thành tiền nếu một bên bị thiếu và có SL
        if (qty is decimal validQty && validQty > 0)
        {
            if (budgetUnitPrice is null && budgetAmount is not null)
                budgetUnitPrice = Math.Round(budgetAmount.Value / validQty, 2);
            else if (budgetUnitPrice is not null && budgetAmount is null)
                budgetAmount = Math.Round(budgetUnitPrice.Value * validQty, 2);

            if (proposalUnitPrice is null && proposalAmount is not null)
                proposalUnitPrice = Math.Round(proposalAmount.Value / validQty, 2);
            else if (proposalUnitPrice is not null && proposalAmount is null)
                proposalAmount = Math.Round(proposalUnitPrice.Value * validQty, 2);

            if (purchaseUnitPrice is null && purchaseAmount is not null)
                purchaseUnitPrice = Math.Round(purchaseAmount.Value / validQty, 2);
            else if (purchaseUnitPrice is not null && purchaseAmount is null)
                purchaseAmount = Math.Round(purchaseUnitPrice.Value * validQty, 2);

            if (selectedUnitPrice is null && selectedAmount is not null)
                selectedUnitPrice = Math.Round(selectedAmount.Value / validQty, 2);
            else if (selectedUnitPrice is not null && selectedAmount is null)
                selectedAmount = Math.Round(selectedUnitPrice.Value * validQty, 2);
        }

        return new PurchasePlanImportLinePreviewDto(
            row,
            lineNo,
            systemName,
            category,
            materialCode,
            productName,
            NullIfEmpty(Get("Spec")),
            unit,
            qty ?? 0m,
            ParseDecimal(Get("KgPerUnit")),
            NullIfEmpty(Get("LineNote")),
            NullIfEmpty(Get("PurchaseStatus")),
            NullIfEmpty(Get("StatusNote")),
            budgetUnitPrice,
            budgetAmount,
            proposalKg,
            proposalUnitPrice,
            proposalAmount,
            NullIfEmpty(Get("ProposalSupplierName")),
            NullIfEmpty(Get("ProposalNote")),
            NullIfEmpty(Get("ProposalStatus")),
            ParseDate(Get("RequiredDate")),
            ParseDate(Get("OrderedDate")),
            ParseDate(Get("ExpectedDeliveryDate")),
            ParseDate(Get("ActualReceiptDate")),
            NullIfEmpty(Get("DeliveryNote")),
            purchaseKg,
            purchaseUnitPrice,
            purchaseAmount,
            NullIfEmpty(Get("PurchaseSupplierName")),
            NullIfEmpty(Get("PurchaseNote")),
            selectedUnitPrice,
            selectedAmount,
            NullIfEmpty(Get("SelectedSupplierName")),
            NullIfEmpty(Get("SelectionReason")),
            quotes,
            isValid,
            lineType,
            NullIfEmpty(Get("Description")));
    }

    private static bool IsBlankRow(
        IXLWorksheet sheet, int row, Dictionary<string, int> fieldMap, List<QuoteGroupColumns> quoteGroups)
    {
        foreach (var col in fieldMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(CellText(sheet.Cell(row, col))))
                return false;
        }

        foreach (var g in quoteGroups)
        {
            foreach (var col in new int?[] { g.KgPerUnitCol, g.UnitPriceCol, g.AmountCol, g.SupplierNameCol, g.NoteCol, g.StatusCol })
            {
                if (col is int c && !string.IsNullOrWhiteSpace(CellText(sheet.Cell(row, c))))
                    return false;
            }
        }

        return true;
    }
}
