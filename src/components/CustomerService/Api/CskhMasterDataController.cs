using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/cskh/master-data")]
[Authorize]
public class CskhMasterDataController(WarehouseDbContext db) : ControllerBase
{
    private const string SettingKey = "cskh_master_data";
    private const string ProfileKey = "cskh_projects_profile";

    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<IActionResult> GetMasterData(CancellationToken ct)
    {
        var setting = await db.WmsSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            var defaultStore = GetDefaultSeedStore();
            setting = new WmsSetting { Key = SettingKey, Value = defaultStore };
            db.WmsSettings.Add(setting);
            await db.SaveChangesAsync(ct);
        }

        return Content(setting.Value, "application/json");
    }

    [HttpPut]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> UpdateMasterData([FromBody] JsonElement payload, CancellationToken ct)
    {
        var jsonString = payload.GetRawText();
        var setting = await db.WmsSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (setting == null)
        {
            db.WmsSettings.Add(new WmsSetting { Key = SettingKey, Value = jsonString });
        }
        else
        {
            setting.Value = jsonString;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("profiles")]
    [RequirePermission("wms.projects.view:all")]
    public async Task<IActionResult> GetProjectProfiles(CancellationToken ct)
    {
        var setting = await db.WmsSettings.FirstOrDefaultAsync(s => s.Key == ProfileKey, ct);
        return Content(setting?.Value ?? "{}", "application/json");
    }

    [HttpPut("profiles")]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> UpdateProjectProfiles([FromBody] JsonElement payload, CancellationToken ct)
    {
        var jsonString = payload.GetRawText();
        var setting = await db.WmsSettings.FirstOrDefaultAsync(s => s.Key == ProfileKey, ct);
        if (setting == null)
        {
            db.WmsSettings.Add(new WmsSetting { Key = ProfileKey, Value = jsonString });
        }
        else
        {
            setting.Value = jsonString;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private static string GetDefaultSeedStore()
    {
        return """
        {
          "industries": [
            { "id": "ind-1", "code": "NN-01", "name": "Hoá chất", "description": "Nhà máy hoá chất, phân bón" },
            { "id": "ind-2", "code": "NN-02", "name": "Dệt may", "description": "Dệt nhuộm, hoàn tất vải sợi" },
            { "id": "ind-3", "code": "NN-03", "name": "Thực phẩm & Đồ uống", "description": "Chế biến thực phẩm, bia rượu nước giải khát" },
            { "id": "ind-4", "code": "NN-04", "name": "Bao bì & Giấy", "description": "Bột giấy, giấy bao bì công nghiệp" },
            { "id": "ind-5", "code": "NN-05", "name": "Gỗ & Nội thất", "description": "Sấy gỗ, ván ép nhân tạo" }
          ],
          "boilerTypes": [
            { "id": "bt-1", "code": "BT-01", "name": "Tầng sôi (CFB)", "description": "Đốt tầng sôi tuần hoàn, hiệu suất cao than cám / biomass" },
            { "id": "bt-2", "code": "BT-02", "name": "Ghi xích (Chain Grate)", "description": "Lò đốt ghi xích nhiên liệu rắn than cục / trấu viên" },
            { "id": "bt-3", "code": "BT-03", "name": "Thu hồi nhiệt (HRSG)", "description": "Thu hồi nhiệt khói thải turbine / dây chuyền luyện thép" },
            { "id": "bt-4", "code": "BT-04", "name": "Dầu tải nhiệt", "description": "Lò dầu truyền nhiệt đốt biomass / than" }
          ],
          "statuses": [
            { "id": "st-1", "code": "ST-01", "name": "Đúng tiến độ", "color": "ok", "description": "Các mốc nghiệm thu diễn ra đúng kế hoạch" },
            { "id": "st-2", "code": "ST-02", "name": "Có nguy cơ trễ", "color": "warn", "description": "Có vướng mắc cần giải tỏa trong 7 ngày" },
            { "id": "st-3", "code": "ST-03", "name": "Chậm tiến độ", "color": "danger", "description": "Trễ mốc bàn giao > 15 ngày, cần can thiệp khẩn" }
          ],
          "priorities": [
            { "id": "pr-1", "code": "PR-01", "name": "Bình thường", "color": "ok", "description": "Tiến độ chuẩn tiêu chuẩn" },
            { "id": "pr-2", "code": "PR-02", "name": "Ưu tiên cao", "color": "warn", "description": "Rút ngắn 15% thời gian chế tạo lắp đặt" },
            { "id": "pr-3", "code": "PR-03", "name": "Khẩn cấp", "color": "danger", "description": "Dự án trọng điểm, kiểm soát tiến độ từng ngày" }
          ],
          "phases": [
            { "id": "ph-1", "code": "PH-01", "name": "Thiết kế & Bóc tách", "order": 1, "active": true, "description": "Thiết kế bản vẽ chế tạo, dự toán vật tư" },
            { "id": "ph-2", "code": "PH-02", "name": "Mua sắm & Gia công", "order": 2, "active": true, "description": "Mua thép, ống, van và chế tạo tại xưởng" },
            { "id": "ph-3", "code": "PH-03", "name": "Vận chuyển & Lắp đặt", "order": 3, "active": true, "description": "Vận chuyển thiết bị tới công trường và lắp đặt kết cấu" },
            { "id": "ph-4", "code": "PH-04", "name": "Thử áp & Chạy thử", "order": 4, "active": true, "description": "Thử áp lực nước, sấy lò và hiệu chỉnh đốt" },
            { "id": "ph-5", "code": "PH-05", "name": "Nghiệm thu bàn giao", "order": 5, "active": true, "description": "Bàn giao hồ sơ kiểm định, nghiệm thu chủ đầu tư" }
          ],
          "schedules": []
        }
        """;
    }
}
