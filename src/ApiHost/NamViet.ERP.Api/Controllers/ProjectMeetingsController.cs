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
    public ActionResult<List<ProjectMeetingDto>> List(string code)
    {
        var list = cskhService.GetMeetings(code);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectMeetingDto> Create(string code, [FromBody] CreateProjectMeetingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Tiêu đề cuộc họp không được để trống." });

        var created = cskhService.CreateMeeting(code, request);
        return Created($"/api/warehouse/projects/{code}/meetings/{created.Id}", created);
    }

    [HttpPut("{id}/minutes")]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectMeetingDto> UpdateMinutes(string code, string id, [FromBody] UpdateProjectMeetingMinutesRequest request)
    {
        var updated = cskhService.UpdateMeetingMinutes(code, id, request);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpPut("{id}/actions/{actionId}")]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectMeetingDto> ToggleAction(string code, string id, string actionId, [FromBody] UpdateProjectMeetingActionRequest request)
    {
        var updated = cskhService.ToggleMeetingAction(code, id, actionId, request.Done);
        if (updated == null) return NotFound();
        return Ok(updated);
    }
}
