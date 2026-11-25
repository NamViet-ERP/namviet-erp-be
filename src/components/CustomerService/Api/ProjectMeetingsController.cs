using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Services.Cskh;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects/{code}/meetings")]
[Authorize]
public class ProjectMeetingsController(ICskhCoordinationService cskhService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectMeetingDto>>> List(string code, CancellationToken ct = default)
    {
        var list = await cskhService.GetMeetingsAsync(code, ct);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectMeetingDto>> Create(
        string code, [FromBody] CreateProjectMeetingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Tiêu đề cuộc họp không được để trống." });

        var created = await cskhService.CreateMeetingAsync(code, request, ct);
        return Created($"/api/warehouse/projects/{code}/meetings/{created.Id}", created);
    }

    [HttpPut("{id}/minutes")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectMeetingDto>> UpdateMinutes(
        string code, string id, [FromBody] UpdateProjectMeetingMinutesRequest request, CancellationToken ct = default)
    {
        var updated = await cskhService.UpdateMeetingMinutesAsync(code, id, request, ct);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpPut("{id}/actions/{actionId}")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectMeetingDto>> ToggleAction(
        string code, string id, string actionId, [FromBody] UpdateProjectMeetingActionRequest request, CancellationToken ct = default)
    {
        var updated = await cskhService.ToggleMeetingActionAsync(code, id, actionId, request.Done, ct);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectMeetingDto>> Update(
        string code, string id, [FromBody] CreateProjectMeetingRequest request, CancellationToken ct = default)
    {
        var updated = await cskhService.UpdateMeetingAsync(code, id, request, ct);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> Delete(string code, string id, CancellationToken ct = default)
    {
        var deleted = await cskhService.DeleteMeetingAsync(code, id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
