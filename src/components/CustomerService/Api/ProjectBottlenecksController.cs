using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects/{code}/bottlenecks")]
[Authorize]
public class ProjectBottlenecksController(ICskhCoordinationService cskhService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectBottleneckDto>>> List(string code, CancellationToken ct = default)
    {
        var list = await cskhService.GetBottlenecksAsync(code, ct);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectBottleneckDto>> Create(
        string code, [FromBody] CreateProjectBottleneckRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Tên điểm nghẽn không được để trống." });

        var created = await cskhService.CreateBottleneckAsync(code, request, ct);
        return Created($"/api/warehouse/projects/{code}/bottlenecks/{created.Id}", created);
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectBottleneckDto>> Update(
        string code, string id, [FromBody] UpdateProjectBottleneckRequest request, CancellationToken ct = default)
    {
        var updated = await cskhService.UpdateBottleneckAsync(code, id, request, ct);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> Delete(string code, string id, CancellationToken ct = default)
    {
        var success = await cskhService.DeleteBottleneckAsync(code, id, ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
