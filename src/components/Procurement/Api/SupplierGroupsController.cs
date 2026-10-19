using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/warehouse/supplier-groups")]
[Authorize]
public class SupplierGroupsController(ISupplierGroupService groups) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierGroupPagedResponse>> List(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await groups.ListAsync(search, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierGroupDto>> GetById(int id, CancellationToken ct) =>
        Ok(await groups.GetAsync(id, ct));

    [HttpPost]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierGroupDto>> Create(
        [FromBody] UpsertSupplierGroupRequest request, CancellationToken ct)
    {
        var created = await groups.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierGroupDto>> Update(
        int id, [FromBody] UpsertSupplierGroupRequest request, CancellationToken ct) =>
        Ok(await groups.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.suppliers.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await groups.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/members")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupMemberDto>>> Members(
        int id, CancellationToken ct) =>
        Ok(await groups.ListMembersAsync(id, ct));

    [HttpPut("{id:int}/members")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupMemberDto>>> SetMembers(
        int id, [FromBody] SetSupplierGroupMembersRequest request, CancellationToken ct) =>
        Ok(await groups.SetMembersAsync(id, request, ct));
}
