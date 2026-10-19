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
    public ActionResult<List<ProjectBottleneckDto>> List(string code)
    {
        var list = cskhService.GetBottlenecks(code);
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectBottleneckDto> Create(string code, [FromBody] CreateProjectBottleneckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Tên điểm nghẽn không được để trống." });

        var created = cskhService.CreateBottleneck(code, request);
        return Created($"/api/warehouse/projects/{code}/bottlenecks/{created.Id}", created);
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.projects.update")]
    public ActionResult<ProjectBottleneckDto> Update(string code, string id, [FromBody] UpdateProjectBottleneckRequest request)
    {
        var updated = cskhService.UpdateBottleneck(code, id, request);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.projects.update")]
    public IActionResult Delete(string code, string id)
    {
        var success = cskhService.DeleteBottleneck(code, id);
        if (!success) return NotFound();
        return NoContent();
    }
}
