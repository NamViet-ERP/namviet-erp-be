using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet("{section}")]
    public async Task<ActionResult<object>> Get(string section, CancellationToken ct)
    {
        var setting = await db.WmsSettings.FirstOrDefaultAsync(s => s.Key == section, ct);
        return Ok(setting?.Value ?? "{}");
    }

    [HttpPut("{section}")]
    public async Task<IActionResult> Put(string section, [FromBody] string value, CancellationToken ct)
    {
        var setting = await db.WmsSettings.FirstOrDefaultAsync(s => s.Key == section, ct);
        if (setting is null)
            db.WmsSettings.Add(new WmsSetting { Key = section, Value = value });
        else
            setting.Value = value;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
