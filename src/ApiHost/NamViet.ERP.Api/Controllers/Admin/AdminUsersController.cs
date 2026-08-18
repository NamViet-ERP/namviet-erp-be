using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Admin;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Application.Services.Admin;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize]
public class AdminUsersController(
    IAdminUserQueryService adminUserQueries,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    HrDbContext hrDb,
    IEmployeeAccountLinkSynchronizer accountLinks) : ControllerBase
{
    [HttpGet]
    [RequirePermission("admin.users.view:all")]
    public async Task<ActionResult<AdminUserPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool pendingOnly = false,
        [FromQuery] string? accountStatus = null,
        [FromQuery] string? source = null,
        [FromQuery] string? department = null,
        CancellationToken ct = default) =>
        Ok(await adminUserQueries.ListAsync(
            new AdminUserListQuery(page, pageSize, search, pendingOnly, accountStatus, source, department),
            ct));

    [HttpGet("{id}")]
    [RequirePermission("admin.users.view:all")]
    public async Task<ActionResult<AdminUserDto>> GetById(
        string id, [FromQuery] string source = "employee", CancellationToken ct = default)
    {
        var row = await adminUserQueries.GetByIdAsync(id, source, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("provision")]
    [RequirePermission("admin.users.create")]
    public async Task<ActionResult<AdminUserDto>> Provision(
        [FromBody] ProvisionAccountRequest request, CancellationToken ct)
    {
        if (!int.TryParse(request.EmployeeId, out var empId))
            return BadRequest("Mã nhân viên không hợp lệ.");

        var employee = await hrDb.Employees
            .Include(e => e.Department)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.Id == empId && e.DeletedAt == null, ct);
        if (employee is null) return NotFound("Không tìm thấy nhân viên.");

        if (employee.Status == EmployeeStatus.Resigned)
            return BadRequest("Không thể cấp tài khoản cho nhân viên đã nghỉ việc.");

        if (employee.UserId is not null)
            return Conflict("Nhân viên đã có tài khoản hệ thống.");

        var existingLink = await userManager.Users
            .FirstOrDefaultAsync(u => u.DeletedAt == null && u.EmployeeCode == employee.Code, ct);
        if (existingLink is not null)
        {
            await accountLinks.AttachByCodeAsync(employee.Code, existingLink.Id, ct);
            if (employee.UserId is null)
                return Conflict("Tài khoản trùng mã NV đã gắn với nhân viên khác.");
            return Ok(await adminUserQueries.MapEmployeeRowAsync(employee, existingLink, ct));
        }

        var email = employee.Email?.Trim();
        if (string.IsNullOrEmpty(email))
            return BadRequest("Nhân viên chưa có email - cập nhật hồ sơ HR trước.");

        if (await userManager.FindByEmailAsync(email) is not null)
            return Conflict($"Email '{email}' đã được sử dụng.");

        var identityRole = WmsMapper.ToIdentityRole(request.Role);
        if (!await roleManager.RoleExistsAsync(identityRole))
            identityRole = "Employee";

        var user = new ApplicationUser
        {
            EmployeeCode = employee.Code,
            Name = employee.FullName,
            Email = email,
            UserName = email,
            PhoneNumber = employee.Phone,
            EmailConfirmed = true,
            IsActive = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, identityRole);
        await accountLinks.AttachByCodeAsync(employee.Code, user.Id, ct);

        return Ok(await adminUserQueries.MapEmployeeRowAsync(employee, user, ct));
    }

    [HttpPost("system")]
    [RequirePermission("admin.users.create")]
    public async Task<ActionResult<AdminUserDto>> CreateSystem(
        [FromBody] CreateSystemAccountRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Conflict($"Email '{email}' đã được sử dụng.");

        var identityRole = WmsMapper.ToIdentityRole(request.Role);
        if (!await roleManager.RoleExistsAsync(identityRole))
            identityRole = "Employee";

        var code = request.EmployeeCode?.Trim();
        if (string.IsNullOrEmpty(code))
            code = $"SYS-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var user = new ApplicationUser
        {
            EmployeeCode = code,
            Name = request.Name.Trim(),
            Email = email,
            UserName = email,
            PhoneNumber = request.Phone?.Trim(),
            EmailConfirmed = true,
            IsActive = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, identityRole);
        await accountLinks.AttachByCodeAsync(user.EmployeeCode, user.Id, ct);

        return Ok(await adminUserQueries.MapManualRowAsync(
            user, ct, request.Department, request.Position));
    }

    [HttpPatch("{id}/status")]
    [RequirePermission("admin.users.lock")]
    public async Task<IActionResult> SetStatus(
        string id, [FromQuery] string source, [FromBody] UpdateAdminUserStatusRequest request, CancellationToken ct)
    {
        ApplicationUser? user;
        if (string.Equals(source, "employee", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(id, out var empId))
                return BadRequest("Mã nhân viên không hợp lệ.");

            var employee = await hrDb.Employees
                .FirstOrDefaultAsync(e => e.Id == empId && e.DeletedAt == null, ct);
            if (employee is null) return NotFound();

            if (employee.Status == EmployeeStatus.Resigned && request.Enabled)
                return BadRequest("Không thể bật tài khoản cho nhân viên đã nghỉ việc.");

            if (employee.UserId is null)
                await accountLinks.SynchronizeAsync(ct);

            if (employee.UserId is null)
                return BadRequest("Nhân viên chưa được cấp tài khoản.");

            user = await userManager.FindByIdAsync(employee.UserId.Value.ToString());
        }
        else
        {
            user = await userManager.Users
                .FirstOrDefaultAsync(u => u.Id.ToString() == id && u.DeletedAt == null, ct);
        }

        if (user is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!request.Enabled
            && !string.IsNullOrEmpty(currentUserId)
            && string.Equals(user.Id.ToString(), currentUserId, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Không thể khóa tài khoản đang đăng nhập.");

        user.IsActive = request.Enabled;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    [HttpPatch("{id}/role")]
    [RequirePermission("admin.users.update")]
    public async Task<ActionResult<AdminUserDto>> SetRole(
        string id, [FromQuery] string source, [FromBody] UpdateAdminUserRoleRequest request, CancellationToken ct)
    {
        var user = await ResolveAccountUserAsync(id, source, requireAccount: true, ct);
        if (user is null) return NotFound();

        var identityRole = WmsMapper.ToIdentityRole(request.Role);
        if (!await roleManager.RoleExistsAsync(identityRole))
            return BadRequest("Vai trò không hợp lệ.");

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return BadRequest(new { errors = removeResult.Errors.Select(e => e.Description) });
        }

        var addResult = await userManager.AddToRoleAsync(user, identityRole);
        if (!addResult.Succeeded)
            return BadRequest(new { errors = addResult.Errors.Select(e => e.Description) });

        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        var row = await adminUserQueries.GetByIdAsync(id, source, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPut("{accountUserId}/password")]
    [RequirePermission("admin.users.update")]
    public async Task<IActionResult> ResetPassword(
        string accountUserId, [FromBody] ResetAdminPasswordRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(accountUserId, out var guid))
            return BadRequest("Mã tài khoản không hợp lệ.");

        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.Id == guid && u.DeletedAt == null, ct);
        if (user is null) return NotFound();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    private async Task<ApplicationUser?> ResolveAccountUserAsync(
        string id, string source, bool requireAccount, CancellationToken ct)
    {
        if (string.Equals(source, "employee", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(id, out var empId)) return null;

            var employee = await hrDb.Employees
                .FirstOrDefaultAsync(e => e.Id == empId && e.DeletedAt == null, ct);
            if (employee is null) return null;

            if (employee.UserId is null)
                await accountLinks.SynchronizeAsync(ct);

            if (requireAccount && employee.UserId is null) return null;
            if (employee.UserId is null) return null;

            return await userManager.FindByIdAsync(employee.UserId.Value.ToString());
        }

        return await userManager.Users
            .FirstOrDefaultAsync(u => u.Id.ToString() == id && u.DeletedAt == null, ct);
    }
}
