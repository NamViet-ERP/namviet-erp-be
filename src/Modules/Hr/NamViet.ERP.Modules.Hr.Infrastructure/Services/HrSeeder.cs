using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Hr.Infrastructure.Services;

public static class HrSeeder
{
    /// <summary>Danh sách vị trí ứng tuyển chuẩn — đồng bộ mỗi lần API khởi động.</summary>
    public static readonly string[] RecruitmentPositions =
    [
        "Cung ứng",
        "CSKH",
        "Lò hơi",
        "Sale KP",
        "Kế toán",
        "Kỹ thuật",
        "Nhân sự",
        "MTKP",
        "Sale KZ",
        "Dự án",
        "Trang trại",
        "Marketing",
        "IT",
        "Sale MZ",
    ];

    public static async Task SeedAsync(HrDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var canonical = RecruitmentPositions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = await db.RecruitmentPositions.ToListAsync(ct);
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var position in all.Where(p => p.DeletedAt == null && !canonical.Contains(p.Name)))
        {
            position.DeletedAt = now;
            position.IsActive = false;
            position.UpdatedAt = now;
            changed = true;
        }

        foreach (var name in RecruitmentPositions)
        {
            var existing = all.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                db.RecruitmentPositions.Add(new RecruitmentPosition { Name = name, IsActive = true });
                changed = true;
                continue;
            }

            if (existing.DeletedAt is not null || !existing.IsActive)
            {
                existing.DeletedAt = null;
                existing.IsActive = true;
                existing.UpdatedAt = now;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Synced {Count} recruitment positions.", RecruitmentPositions.Length);
        }
    }
}
