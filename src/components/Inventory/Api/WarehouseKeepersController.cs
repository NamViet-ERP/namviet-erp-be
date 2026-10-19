using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    /// <summary>Danh sách người dùng có thể gán phụ trách kho dự án (ưu tiên Thủ kho, kèm toàn bộ tài khoản active).</summary>
    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<IReadOnlyList<ProjectWarehouseKeeperDto>>> List(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var keeperUsers = await userManager.GetUsersInRoleAsync(RolePermissionTemplates.WarehouseRole);
        var keeperUserIds = keeperUsers
            .Where(u => u.DeletedAt == null && u.IsActive)
            .Select(u => u.Id)
            .ToHashSet();

        var query = userManager.Users.Where(u => u.DeletedAt == null && u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(u =>
                (u.Name != null && EF.Functions.ILike(u.Name, $"%{q}%"))
                || (u.Email != null && EF.Functions.ILike(u.Email, $"%{q}%"))
                || (u.EmployeeCode != null && EF.Functions.ILike(u.EmployeeCode, $"%{q}%")));
        }

        var allUsers = await query.ToListAsync(ct);

        var items = allUsers
            .OrderByDescending(u => keeperUserIds.Contains(u.Id))
            .ThenBy(u => u.Name)
            .Select(u => new ProjectWarehouseKeeperDto(
                u.Id,
                string.IsNullOrWhiteSpace(u.Name) ? (u.Email ?? u.Id.ToString()) : u.Name!))
            .ToList();

        return Ok(items);
    }
}
