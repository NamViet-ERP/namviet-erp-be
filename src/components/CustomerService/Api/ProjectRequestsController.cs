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
    public ActionResult<List<ProjectRequestCenterDto>> List(string code)
    {
        var list = cskhService.GetRequests(code);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectRequestCenterDto> Create(string code, [FromBody] CreateProjectRequestCenterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Tên yêu cầu phát sinh không được để trống." });

        var created = cskhService.CreateRequest(code, request);
        return Created($"/api/warehouse/projects/{code}/requests/{created.Id}", created);
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectRequestCenterDto> Update(string code, string id, [FromBody] UpdateProjectRequestCenterRequest request)
    {
        var updated = cskhService.UpdateRequest(code, id, request);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.projects.update")]
    public IActionResult Delete(string code, string id)
    {
        var deleted = cskhService.DeleteRequest(code, id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
