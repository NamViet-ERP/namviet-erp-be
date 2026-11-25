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
    private const string EmptyStoreJson =
        "{\"industries\":[],\"boilerTypes\":[],\"statuses\":[],\"priorities\":[],\"phases\":[],\"schedules\":[]}";

    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<IActionResult> GetMasterData(CancellationToken ct)
    {
        var setting = await db.WmsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return Content(EmptyStoreJson, "application/json");
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
        var setting = await db.WmsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == ProfileKey, ct);
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
}
