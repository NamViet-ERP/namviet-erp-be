using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Admin;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Admin;

public class AdminUserQueryService(
    UserManager<ApplicationUser> userManager,
    IdentityDbContext identityDb,
    HrDbContext hrDb) : IAdminUserQueryService
{
    public async Task<AdminUserPagedResponse> ListAsync(AdminUserListQuery query, CancellationToken ct = default)
    {
        var summary = await GetSummaryAsync(ct);

        var allRows = await BuildAllRowsAsync(ct);
        IEnumerable<AdminUserDto> filtered = allRows;

        if (query.PendingOnly)
            filtered = filtered.Where(r => r.Source == "employee" && !r.HasAccount);
        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(r => MatchesSearch(r, query.Search));
        if (!string.IsNullOrWhiteSpace(query.AccountStatus))
            filtered = filtered.Where(r => MatchesAccountStatus(r, query.AccountStatus));
        if (!string.IsNullOrWhiteSpace(query.Source))
            filtered = filtered.Where(r => MatchesSource(r, query.Source));
        if (!string.IsNullOrWhiteSpace(query.Department))
            filtered = filtered.Where(r => MatchesDepartment(r, query.Department));

        var list = filtered.ToList();
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = list.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        var items = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AdminUserPagedResponse(items, total, page, pageSize, totalPages, summary);
    }

    public async Task<AdminUserDto?> GetByIdAsync(string id, string source, CancellationToken ct = default)
    {
        if (string.Equals(source, "employee", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(id, out var empId))
                return null;

            var employee = await hrDb.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == empId && e.DeletedAt == null, ct);
            if (employee is null)
                return null;

            ApplicationUser? linked = null;
            if (employee.UserId is not null)
                linked = await userManager.FindByIdAsync(employee.UserId.Value.ToString());
            else
            {
                linked = await userManager.Users
                    .FirstOrDefaultAsync(u => u.DeletedAt == null && u.EmployeeCode == employee.Code, ct);
            }

            var lookups = linked is not null
                ? await BuildLookupsAsync([linked], ct)
                : await BuildLookupsAsync([], ct);
            return ToEmployeeRow(employee, linked, lookups);
        }

        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.Id.ToString() == id && u.DeletedAt == null, ct);
        if (user is null)
            return null;

        var manualLookups = await BuildLookupsAsync([user], ct);
        return ToManualRow(user, manualLookups);
    }

    public async Task<AdminUserListSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var employees = await hrDb.Employees
            .AsNoTracking()
            .Where(e => e.DeletedAt == null)
            .Select(e => new { e.Id, e.Code, e.UserId, e.Status })
            .ToListAsync(ct);

        var users = await userManager.Users
            .AsNoTracking()
            .Where(u => u.DeletedAt == null)
            .Select(u => new { u.Id, u.EmployeeCode, u.IsActive })
            .ToListAsync(ct);

        var userById = users.ToDictionary(u => u.Id);
        var usersByEmployeeCode = users
            .Where(u => !string.IsNullOrEmpty(u.EmployeeCode))
            .GroupBy(u => u.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var linkedUserIds = new HashSet<Guid>();
        var pending = 0;
        var active = 0;
        var inactive = 0;

        foreach (var emp in employees)
        {
            var linked = emp.UserId is Guid uid && userById.TryGetValue(uid, out var byId)
                ? byId
                : usersByEmployeeCode.GetValueOrDefault(emp.Code);

            if (linked is null)
            {
                pending++;
                continue;
            }

            linkedUserIds.Add(linked.Id);
            var isActive = linked.IsActive && emp.Status != EmployeeStatus.Resigned;
            if (isActive) active++;
            else inactive++;
        }

        foreach (var user in users.Where(u => !linkedUserIds.Contains(u.Id)))
        {
            if (user.IsActive) active++;
            else inactive++;
        }

        var total = employees.Count + users.Count(u => !linkedUserIds.Contains(u.Id));
        return new AdminUserListSummaryDto(total, pending, active, inactive);
    }

    public async Task<AdminUserDto> MapEmployeeRowAsync(
        Employee employee, ApplicationUser linked, CancellationToken ct)
    {
        var lookups = await BuildLookupsAsync([linked], ct);
        return ToEmployeeRow(employee, linked, lookups);
    }

    public async Task<AdminUserDto> MapManualRowAsync(
        ApplicationUser user,
        CancellationToken ct,
        string? department = null,
        string? position = null)
    {
        var lookups = await BuildLookupsAsync([user], ct);
        return ToManualRow(user, lookups, department, position);
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
            .OrderBy(r => r, Comparer<AdminUserDto>.Create((a, b) =>
            {
                var cmp = HrEmployeeMapper.CompareEmployeeCodeNewestFirst(a.EmployeeCode, b.EmployeeCode);
                return cmp != 0 ? cmp : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            }))
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

    private static AdminUserDto ToManualRow(
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
