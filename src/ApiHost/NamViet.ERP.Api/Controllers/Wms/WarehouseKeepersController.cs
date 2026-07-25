using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/warehouse/keepers")]
[Authorize]
public class WarehouseKeepersController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>Danh sách user role Thủ kho — dùng gợi ý gán phụ trách dự án.</summary>
    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<IReadOnlyList<ProjectWarehouseKeeperDto>>> List(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var users = await userManager.GetUsersInRoleAsync(RolePermissionTemplates.WarehouseRole);
        IEnumerable<ApplicationUser> filtered = users.Where(u => u.DeletedAt == null && u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            filtered = filtered.Where(u =>
                (u.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (u.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (u.EmployeeCode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var items = filtered
            .OrderBy(u => u.Name)
            .Select(u => new ProjectWarehouseKeeperDto(
                u.Id,
                string.IsNullOrWhiteSpace(u.Name) ? (u.Email ?? u.Id.ToString()) : u.Name!))
            .ToList();

        return Ok(items);
    }
}
