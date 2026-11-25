using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects/{code}/requests")]
[Authorize]
public class ProjectRequestsController(ICskhCoordinationService cskhService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectRequestCenterDto>>> List(string code, CancellationToken ct = default)
    {
        var list = await cskhService.GetRequestsAsync(code, ct);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectRequestCenterDto>> Create(
        string code, [FromBody] CreateProjectRequestCenterRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Tên yêu cầu phát sinh không được để trống." });

        var created = await cskhService.CreateRequestAsync(code, request, ct);
        return Created($"/api/warehouse/projects/{code}/requests/{created.Id}", created);
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectRequestCenterDto>> Update(
        string code, string id, [FromBody] UpdateProjectRequestCenterRequest request, CancellationToken ct = default)
    {
        var updated = await cskhService.UpdateRequestAsync(code, id, request, ct);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> Delete(string code, string id, CancellationToken ct = default)
    {
        var deleted = await cskhService.DeleteRequestAsync(code, id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
