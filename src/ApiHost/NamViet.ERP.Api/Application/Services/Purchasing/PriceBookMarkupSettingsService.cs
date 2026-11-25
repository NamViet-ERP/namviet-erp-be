using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPriceBookMarkupSettingsService
{
    Task<PriceBookMarkupSettingsDto> GetAsync(CancellationToken ct = default);

    Task<PriceBookMarkupSettingsDto> UpdateAsync(
        UpdatePriceBookMarkupSettingsRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Cấu hình hệ số markup chào thầu lưu trong wms_settings dưới khóa pricebook_markup_settings.
/// Chưa có dòng nào thì trả mặc định rỗng/0 trong memory — tuyệt đối không tự seed DB khi GET.
/// </summary>
public sealed class PriceBookMarkupSettingsService(
    WarehouseDbContext db,
    ICurrentUser currentUser) : IPriceBookMarkupSettingsService
{
    public static readonly PriceBookMarkupSettingsDto Defaults = new(
        GlobalMarkupPercent: 0m,
        Categories: [],
        ItemOverrides: new Dictionary<string, decimal>(StringComparer.Ordinal),
        UpdatedAt: null,
        UpdatedBy: null);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PriceBookMarkupSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var row = await db.WmsSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == WmsSettingsKeys.PriceBookMarkup, ct);

        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return Defaults;

        try
        {
            var parsed = JsonSerializer.Deserialize<PriceBookMarkupSettingsDto>(row.Value, JsonOptions);
            if (parsed is null)
                return Defaults;

            return new PriceBookMarkupSettingsDto(
                parsed.GlobalMarkupPercent,
                parsed.Categories ?? [],
                parsed.ItemOverrides ?? new Dictionary<string, decimal>(StringComparer.Ordinal),
                parsed.UpdatedAt,
                parsed.UpdatedBy);
        }
        catch (JsonException)
        {
            return Defaults;
        }
    }

    public async Task<PriceBookMarkupSettingsDto> UpdateAsync(
        UpdatePriceBookMarkupSettingsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GlobalMarkupPercent is < 0m or > 100m)
            throw new DomainRuleException("Tỷ lệ markup mặc định phải nằm trong khoảng 0 đến 100%.");

        var categories = new List<PriceBookMarkupCategoryDto>();
        var seenCategoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Categories ?? [])
        {
            var id = item.Id?.Trim() ?? string.Empty;
            var name = item.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id))
                throw new DomainRuleException("Mã nhóm hàng cấu hình markup không được để trống.");
            if (string.IsNullOrEmpty(name))
                throw new DomainRuleException("Tên nhóm hàng cấu hình markup không được để trống.");
            if (item.Percent is < 0m or > 100m)
                throw new DomainRuleException($"Tỷ lệ markup của nhóm '{name}' phải nằm trong khoảng 0 đến 100%.");
            if (!seenCategoryIds.Add(id))
                throw new DomainRuleException($"Nhóm hàng '{id}' bị trùng lặp trong cấu hình markup.");

            categories.Add(new PriceBookMarkupCategoryDto(
                id,
                name,
                item.Percent,
                string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim()));
        }

        var itemOverrides = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var kv in request.ItemOverrides ?? new Dictionary<string, decimal>())
        {
            var code = kv.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(code))
                throw new DomainRuleException("Mã vật tư ghi đè markup không được để trống.");
            if (kv.Value is < 0m or > 100m)
                throw new DomainRuleException($"Tỷ lệ markup của vật tư '{code}' phải nằm trong khoảng 0 đến 100%.");

            itemOverrides[code] = kv.Value;
        }

        var now = DateTime.UtcNow;
        var updatedBy = !string.IsNullOrWhiteSpace(currentUser.Name)
            ? currentUser.Name
            : !string.IsNullOrWhiteSpace(currentUser.Email)
                ? currentUser.Email
                : "Quản trị";

        var value = new PriceBookMarkupSettingsDto(
            request.GlobalMarkupPercent,
            categories,
            itemOverrides,
            now,
            updatedBy);

        var serialized = JsonSerializer.Serialize(value, JsonOptions);
        var row = await db.WmsSettings
            .FirstOrDefaultAsync(s => s.Key == WmsSettingsKeys.PriceBookMarkup, ct);

        if (row is null)
        {
            db.WmsSettings.Add(new WmsSetting
            {
                Key = WmsSettingsKeys.PriceBookMarkup,
                Value = serialized,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            row.Value = serialized;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return value;
    }
}
