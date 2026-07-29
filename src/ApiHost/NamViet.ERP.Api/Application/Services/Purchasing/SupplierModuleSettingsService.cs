using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierModuleSettingsService
{
    Task<SupplierModuleSettingsDto> GetAsync(CancellationToken ct = default);

    Task<SupplierModuleSettingsDto> UpdateAsync(
        UpdateSupplierModuleSettingsRequest request, CancellationToken ct = default);
}

/// <summary>
/// Thiết lập module NCC lưu trong wms_settings dưới một dòng JSON.
/// Chưa có dòng nào thì trả mặc định trong code — không seed DB.
/// </summary>
public sealed class SupplierModuleSettingsService(WarehouseDbContext db) : ISupplierModuleSettingsService
{
    public static readonly SupplierModuleSettingsDto Defaults = new(
        GradeAMinScore: 90m,
        GradeBMinScore: 80m,
        GradeCMinScore: 70m,
        GradeDMinScore: 60m,
        GradeEMinScore: 50m,
        GradeFMinScore: 40m,
        GradeGMinScore: 30m,
        GradeHMinScore: 20m,
        EvaluationCycle: SupplierEvaluationCycles.Quarterly,
        GroupRequired: false);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SupplierModuleSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var row = await db.WmsSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == WmsSettingsKeys.SupplierModule, ct);

        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return Defaults;

        try
        {
            var parsed = JsonSerializer.Deserialize<SupplierModuleSettingsDto>(row.Value, JsonOptions);
            return CoalesceLegacy(parsed) ?? Defaults;
        }
        catch (JsonException)
        {
            // Giá trị hỏng không được làm sập trang thiết lập.
            return Defaults;
        }
    }

    public async Task<SupplierModuleSettingsDto> UpdateAsync(
        UpdateSupplierModuleSettingsRequest request, CancellationToken ct = default)
    {
        var cycle = (request.EvaluationCycle ?? string.Empty).Trim().ToLowerInvariant();
        if (!SupplierEvaluationCycles.All.Contains(cycle))
            throw new PurchasingConflictException("Chu kỳ đánh giá phải là monthly, quarterly hoặc yearly.");

        var thresholds = new[]
        {
            request.GradeAMinScore,
            request.GradeBMinScore,
            request.GradeCMinScore,
            request.GradeDMinScore,
            request.GradeEMinScore,
            request.GradeFMinScore,
            request.GradeGMinScore,
            request.GradeHMinScore,
        };
        if (thresholds.Any(t => t is < 0 or > 100))
            throw new PurchasingConflictException("Ngưỡng điểm phải nằm trong khoảng 0–100.");
        for (var i = 0; i < thresholds.Length - 1; i++)
        {
            if (thresholds[i] <= thresholds[i + 1])
                throw new PurchasingConflictException(
                    $"Ngưỡng hạng {SupplierGrades.All[i]} phải lớn hơn ngưỡng hạng {SupplierGrades.All[i + 1]}.");
        }

        var value = new SupplierModuleSettingsDto(
            request.GradeAMinScore,
            request.GradeBMinScore,
            request.GradeCMinScore,
            request.GradeDMinScore,
            request.GradeEMinScore,
            request.GradeFMinScore,
            request.GradeGMinScore,
            request.GradeHMinScore,
            cycle,
            request.GroupRequired);

        var row = await db.WmsSettings
            .FirstOrDefaultAsync(s => s.Key == WmsSettingsKeys.SupplierModule, ct);

        if (row is null)
        {
            db.WmsSettings.Add(new WmsSetting
            {
                Key = WmsSettingsKeys.SupplierModule,
                Value = JsonSerializer.Serialize(value, JsonOptions),
            });
        }
        else
        {
            row.Value = JsonSerializer.Serialize(value, JsonOptions);
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return value;
    }

    /// <summary>
    /// JSON cũ chỉ có A/B → C–H = 0; điền mặc định bậc thang dưới B để không phá trang/đánh giá.
    /// </summary>
    private static SupplierModuleSettingsDto? CoalesceLegacy(SupplierModuleSettingsDto? d)
    {
        if (d is null) return null;

        var hasExtended = d.GradeCMinScore > 0
            || d.GradeDMinScore > 0
            || d.GradeEMinScore > 0
            || d.GradeFMinScore > 0
            || d.GradeGMinScore > 0
            || d.GradeHMinScore > 0;

        if (hasExtended)
            return d;

        var a = d.GradeAMinScore > 0 ? d.GradeAMinScore : Defaults.GradeAMinScore;
        var b = d.GradeBMinScore > 0 ? d.GradeBMinScore : Defaults.GradeBMinScore;
        if (a <= b)
        {
            a = Defaults.GradeAMinScore;
            b = Defaults.GradeBMinScore;
        }

        var step = Math.Max(5m, Math.Round(b / 7m, 2));
        decimal Next(ref decimal prev)
        {
            prev = Math.Max(0m, Math.Round(prev - step, 2));
            return prev;
        }

        var cursor = b;
        var c = Next(ref cursor);
        var dScore = Next(ref cursor);
        var e = Next(ref cursor);
        var f = Next(ref cursor);
        var g = Next(ref cursor);
        var h = Next(ref cursor);

        return new SupplierModuleSettingsDto(
            a,
            b,
            c,
            dScore,
            e,
            f,
            g,
            h,
            string.IsNullOrWhiteSpace(d.EvaluationCycle) ? Defaults.EvaluationCycle : d.EvaluationCycle,
            d.GroupRequired);
    }
}
