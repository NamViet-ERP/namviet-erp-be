using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Admin;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IdentityDbContext identityDb,
    HrDbContext hrDb) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminUserPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool pendingOnly = false,
        [FromQuery] string? accountStatus = null,
        [FromQuery] string? source = null,
        [FromQuery] string? department = null,
        CancellationToken ct = default)
    {
        var allRows = await BuildAllRowsAsync(ct);
        var summary = new AdminUserListSummaryDto(
            allRows.Count,
            allRows.Count(r => r.Source == "employee" && !r.HasAccount),
            allRows.Count(r => r.HasAccount && r.IsActive),
            allRows.Count(r => r.HasAccount && !r.IsActive));

        IEnumerable<AdminUserDto> filtered = allRows;
        if (pendingOnly)
            filtered = filtered.Where(r => r.Source == "employee" && !r.HasAccount);
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(r => MatchesSearch(r, search));
        if (!string.IsNullOrWhiteSpace(accountStatus))
            filtered = filtered.Where(r => MatchesAccountStatus(r, accountStatus));
        if (!string.IsNullOrWhiteSpace(source))
            filtered = filtered.Where(r => MatchesSource(r, source));
        if (!string.IsNullOrWhiteSpace(department))
            filtered = filtered.Where(r => MatchesDepartment(r, department));

        var list = filtered.ToList();
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = list.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new AdminUserPagedResponse(items, total, page, pageSize, totalPages, summary));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminUserDto>> GetById(
        string id, [FromQuery] string source = "employee", CancellationToken ct = default)
    {
        var allRows = await BuildAllRowsAsync(ct);
        var row = allRows.FirstOrDefault(r =>
            string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase));
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("provision")]
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
            return Conflict("Nhân viên đã có tài khoản hệ thống.");

        var email = employee.Email?.Trim();
        if (string.IsNullOrEmpty(email))
            return BadRequest("Nhân viên chưa có email — cập nhật hồ sơ HR trước.");

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

        employee.UserId = user.Id;
        employee.UpdatedAt = DateTime.UtcNow;
        await hrDb.SaveChangesAsync(ct);

        var lookups = await BuildLookupsAsync([user], ct);
        return Ok(ToEmployeeRow(employee, user, lookups));
    }

    [HttpPost("system")]
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

        var lookups = await BuildLookupsAsync([user], ct);
        return Ok(ToManualRow(user, lookups, request.Department, request.Position));
    }

    [HttpPatch("{id}/status")]
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
                return BadRequest("Nhân viên chưa được cấp tài khoản.");

            user = await userManager.FindByIdAsync(employee.UserId.Value.ToString());
        }
        else
        {
            user = await userManager.Users
                .FirstOrDefaultAsync(u => u.Id.ToString() == id && u.DeletedAt == null, ct);
        }

        if (user is null) return NotFound();

        user.IsActive = request.Enabled;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    [HttpPatch("{id}/role")]
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

        var allRows = await BuildAllRowsAsync(ct);
        var row = allRows.FirstOrDefault(r =>
            string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase));
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPut("{accountUserId}/password")]
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

    private sealed record UserLookups(
        Dictionary<Guid, List<string>> RolesByUserId,
        Dictionary<int, ApplicationUser> UsersByEmployeeId,
        Dictionary<string, ApplicationUser> UsersByEmployeeCode,
        Dictionary<string, string> RoleLabelByIdentityName);

    private async Task<List<AdminUserDto>> BuildAllRowsAsync(CancellationToken ct)
    {
        var employees = await hrDb.Employees
            .Include(e => e.Department)
            .Include(e => e.Position)
            .Where(e => e.DeletedAt == null)
            .OrderBy(e => e.FullName)
            .ToListAsync(ct);

        var users = await userManager.Users
            .Where(u => u.DeletedAt == null)
            .ToListAsync(ct);

        var lookups = await BuildLookupsAsync(users, ct);
        var linkedUserIds = new HashSet<Guid>();

        var rows = new List<AdminUserDto>();
        foreach (var emp in employees)
        {
            lookups.UsersByEmployeeId.TryGetValue(emp.Id, out var linked);
            linked ??= lookups.UsersByEmployeeCode.GetValueOrDefault(emp.Code);
            if (linked is not null) linkedUserIds.Add(linked.Id);
            rows.Add(ToEmployeeRow(emp, linked, lookups));
        }

        foreach (var user in users
                     .Where(u => !linkedUserIds.Contains(u.Id))
                     .OrderBy(u => u.Name)
                     .ThenBy(u => u.Id))
            rows.Add(ToManualRow(user, lookups));

        return rows
            .OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesSearch(AdminUserDto row, string search)
    {
        var q = search.Trim();
        return ContainsIgnoreCase(row.FullName, q)
            || ContainsIgnoreCase(row.Email, q)
            || ContainsIgnoreCase(row.EmployeeCode, q)
            || ContainsIgnoreCase(row.Phone, q)
            || ContainsIgnoreCase(row.Department, q)
            || ContainsIgnoreCase(row.Position, q)
            || ContainsIgnoreCase(row.Role, q)
            || ContainsIgnoreCase(row.StatusLabel, q);
    }

    private static bool MatchesAccountStatus(AdminUserDto row, string accountStatus)
    {
        var status = accountStatus.Trim().ToLowerInvariant();
        return status switch
        {
            "pending" => !row.HasAccount,
            "active" => row.HasAccount && row.IsActive,
            "locked" => row.HasAccount && !row.IsActive,
            _ => true,
        };
    }

    private static bool MatchesSource(AdminUserDto row, string source)
    {
        var value = source.Trim().ToLowerInvariant();
        return value is "employee" or "manual"
            && string.Equals(row.Source, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDepartment(AdminUserDto row, string department)
    {
        var q = department.Trim();
        return ContainsIgnoreCase(row.Department, q);
    }

    private static bool ContainsIgnoreCase(string? value, string query)
        => !string.IsNullOrEmpty(value) &&
           value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task<ApplicationUser?> ResolveAccountUserAsync(
        string id, string source, bool requireAccount, CancellationToken ct)
    {
        if (string.Equals(source, "employee", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(id, out var empId)) return null;

            var employee = await hrDb.Employees
                .FirstOrDefaultAsync(e => e.Id == empId && e.DeletedAt == null, ct);
            if (employee is null) return null;
            if (requireAccount && employee.UserId is null) return null;

            if (employee.UserId is null)
            {
                var linked = await userManager.Users
                    .FirstOrDefaultAsync(u => u.DeletedAt == null && u.EmployeeCode == employee.Code, ct);
                return linked;
            }

            return await userManager.FindByIdAsync(employee.UserId.Value.ToString());
        }

        return await userManager.Users
            .FirstOrDefaultAsync(u => u.Id.ToString() == id && u.DeletedAt == null, ct);
    }

    private async Task<UserLookups> BuildLookupsAsync(List<ApplicationUser> users, CancellationToken ct)
    {
        var userIds = users.Select(u => u.Id).ToList();
        var codes = users.Select(u => u.EmployeeCode).Where(c => !string.IsNullOrEmpty(c)).ToList();

        var roleRows = await (
            from ur in identityDb.UserRoles
            join r in identityDb.Roles on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name! }
        ).ToListAsync(ct);

        var rolesByUserId = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        var allRoles = await identityDb.Roles.ToListAsync(ct);
        var roleLabelByIdentityName = allRoles
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .ToDictionary(
                r => r.Name!,
                r => !string.IsNullOrWhiteSpace(r.Description)
                    ? r.Description!
                    : WmsMapper.ToClientRoleLabel(r.Name!),
                StringComparer.OrdinalIgnoreCase);

        var employees = await hrDb.Employees
            .Where(e => e.DeletedAt == null &&
                ((e.UserId != null && userIds.Contains(e.UserId.Value)) || codes.Contains(e.Code)))
            .ToListAsync(ct);

        var usersByEmployeeId = new Dictionary<int, ApplicationUser>();
        foreach (var e in employees.Where(e => e.UserId.HasValue))
        {
            var matched = users.FirstOrDefault(u => u.Id == e.UserId!.Value);
            if (matched is not null) usersByEmployeeId[e.Id] = matched;
        }

        var usersByEmployeeCode = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in employees)
        {
            if (usersByEmployeeId.ContainsKey(e.Id)) continue;
            var matched = users.FirstOrDefault(u =>
                string.Equals(u.EmployeeCode, e.Code, StringComparison.OrdinalIgnoreCase));
            if (matched is not null) usersByEmployeeCode[e.Code] = matched;
        }

        return new UserLookups(rolesByUserId, usersByEmployeeId, usersByEmployeeCode, roleLabelByIdentityName);
    }

    private static string ResolveRoleLabel(ApplicationUser user, UserLookups lookups)
    {
        var roles = lookups.RolesByUserId.GetValueOrDefault(user.Id) ?? [];
        var identityRole = roles.FirstOrDefault() ?? "Employee";
        if (lookups.RoleLabelByIdentityName.TryGetValue(identityRole, out var label))
            return label;
        return WmsMapper.ToClientRoleLabel(identityRole);
    }

    private static string ResolveRoleCode(ApplicationUser user, UserLookups lookups)
    {
        var roles = lookups.RolesByUserId.GetValueOrDefault(user.Id) ?? [];
        var identityRole = roles.FirstOrDefault() ?? "Employee";
        return WmsMapper.ToClientRole(identityRole);
    }

    private static AdminUserDto ToEmployeeRow(Employee emp, ApplicationUser? linked, UserLookups lookups)
    {
        var resigned = emp.Status == EmployeeStatus.Resigned;
        var hasAccount = linked is not null;

        if (!hasAccount)
        {
            return new AdminUserDto(
                emp.Id.ToString(), emp.Id.ToString(), null, "employee", false,
                emp.Code, emp.FullName,
                emp.Email ?? "—", emp.Phone ?? "—",
                emp.Department?.Name ?? "—", emp.Position?.Name ?? "—",
                "—", "—", "Chưa cấp tài khoản", false, false, !resigned,
                HrEmployeeMapper.FormatDisplayDate(emp.JoinDate));
        }

        var isActive = linked!.IsActive && emp.Status != EmployeeStatus.Resigned;
        return new AdminUserDto(
            emp.Id.ToString(), emp.Id.ToString(), linked.Id.ToString(), "employee", true,
            emp.Code, emp.FullName,
            linked.Email ?? emp.Email ?? "—", emp.Phone ?? linked.PhoneNumber ?? "—",
            emp.Department?.Name ?? "—", emp.Position?.Name ?? "—",
            ResolveRoleLabel(linked, lookups),
            ResolveRoleCode(linked, lookups),
            isActive ? "Hoạt động" : "Khóa",
            isActive, !resigned, false,
            HrEmployeeMapper.FormatDisplayDate(emp.JoinDate));
    }

    private AdminUserDto ToManualRow(
        ApplicationUser user, UserLookups lookups, string? department = null, string? position = null)
    {
        var isActive = user.IsActive;
        return new AdminUserDto(
            user.Id.ToString(), user.Id.ToString(), user.Id.ToString(), "manual", true,
            user.EmployeeCode, user.Name,
            user.Email ?? "—", user.PhoneNumber ?? "—",
            department ?? "—", position ?? "—",
            ResolveRoleLabel(user, lookups),
            ResolveRoleCode(user, lookups),
            isActive ? "Hoạt động" : "Khóa",
            isActive, true, false,
            WmsMapper.FormatDate(user.CreatedAt));
    }
}
