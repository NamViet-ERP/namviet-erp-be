using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/members")]
[Authorize]
public class MembersController(
    UserManager<ApplicationUser> userManager,
    IdentityDbContext identityDb,
    HrDbContext hrDb,
    IEmployeeUserLinkService employeeLinks) : ControllerBase
{
    private static readonly Dictionary<string, string> ClientRoleToIdentity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = "Admin",
        ["storekeeper"] = "Warehouse_Keeper",
        ["accountant"] = "Accountant",
        ["procurement"] = "Procurement",
        ["pm"] = "PM"
    };

    [HttpGet]
    public async Task<ActionResult<List<MemberDto>>> List(CancellationToken ct)
    {
        var users = await userManager.Users.Where(u => u.DeletedAt == null).OrderBy(u => u.Name).ToListAsync(ct);
        var lookups = await BuildMemberLookupsAsync(users, ct);
        return Ok(users.Select(u => ToMemberDto(u, lookups, null, null)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MemberDto>> GetById(string id, CancellationToken ct)
    {
        var user = await FindUserAsync(id, ct);
        if (user is null) return NotFound();
        var lookups = await BuildMemberLookupsAsync([user], ct);
        return Ok(ToMemberDto(user, lookups, null, null));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<MemberDto>> Create([FromBody] CreateMemberDto dto, CancellationToken ct)
    {
        if (await userManager.Users.AnyAsync(u => u.EmployeeCode == dto.Code, ct))
            return Conflict($"Mã nhân viên '{dto.Code}' đã tồn tại.");

        var user = new ApplicationUser
        {
            EmployeeCode = dto.Code,
            Name = dto.Name,
            Email = dto.Email,
            UserName = dto.Email,
            PhoneNumber = dto.Phone,
            EmailConfirmed = true,
            IsActive = dto.Status != "inactive"
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        var identityRole = ClientRoleToIdentity.GetValueOrDefault(dto.Role, "Employee");
        await userManager.AddToRoleAsync(user, identityRole);

        await employeeLinks.UpsertFromUserAsync(new EmployeeUserLinkRequest(
            user.Id, user.EmployeeCode, user.Name, user.Email, dto.Phone, dto.JoinedDate,
            "DEFAULT", "Mặc định"), ct);

        var lookups = await BuildMemberLookupsAsync([user], ct);
        return Ok(ToMemberDto(user, lookups, dto.Role, dto.RoleLabel));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<MemberDto>> Update(string id, [FromBody] UpdateMemberDto dto, CancellationToken ct)
    {
        var user = await FindUserAsync(id, ct);
        if (user is null) return NotFound();

        if (dto.Code is not null) user.EmployeeCode = dto.Code;
        if (dto.Name is not null) user.Name = dto.Name;
        if (dto.Email is not null) { user.Email = dto.Email; user.UserName = dto.Email; }
        if (dto.Phone is not null) user.PhoneNumber = dto.Phone;
        if (dto.Status is not null) user.IsActive = dto.Status != "inactive";
        user.UpdatedAt = DateTime.UtcNow;

        await userManager.UpdateAsync(user);

        await employeeLinks.UpsertFromUserAsync(new EmployeeUserLinkRequest(
            user.Id, user.EmployeeCode, user.Name, user.Email, dto.Phone, dto.JoinedDate,
            "DEFAULT", "Mặc định"), ct);

        if (!string.IsNullOrEmpty(dto.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await userManager.ResetPasswordAsync(user, token, dto.Password);
        }

        if (dto.Role is not null)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            var identityRole = ClientRoleToIdentity.GetValueOrDefault(dto.Role, "Employee");
            await userManager.AddToRoleAsync(user, identityRole);
        }

        var lookups = await BuildMemberLookupsAsync([user], ct);
        return Ok(ToMemberDto(user, lookups, dto.Role, dto.RoleLabel));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var user = await FindUserAsync(id, ct);
        if (user is null) return NotFound();
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    private async Task<ApplicationUser?> FindUserAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await userManager.Users.FirstOrDefaultAsync(u => u.Id == guid && u.DeletedAt == null, ct);
        return await userManager.Users.FirstOrDefaultAsync(u => u.EmployeeCode == id && u.DeletedAt == null, ct);
    }

    private sealed record MemberLookups(
        Dictionary<Guid, List<string>> RolesByUserId,
        Dictionary<Guid, Employee> EmployeeByUserId,
        Dictionary<string, Employee> EmployeeByCode);

    private async Task<MemberLookups> BuildMemberLookupsAsync(
        List<ApplicationUser> users, CancellationToken ct)
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

        var employees = await hrDb.Employees
            .Include(e => e.Department)
            .Where(e => (e.UserId != null && userIds.Contains(e.UserId.Value)) || codes.Contains(e.Code))
            .ToListAsync(ct);

        var employeeByUserId = employees
            .Where(e => e.UserId.HasValue)
            .GroupBy(e => e.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var employeeByCode = employees
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return new MemberLookups(rolesByUserId, employeeByUserId, employeeByCode);
    }

    private static MemberDto ToMemberDto(
        ApplicationUser user,
        MemberLookups lookups,
        string? clientRoleOverride,
        string? roleLabelOverride)
    {
        var roles = lookups.RolesByUserId.GetValueOrDefault(user.Id) ?? [];
        var identityRole = roles.FirstOrDefault(r => ClientRoleToIdentity.ContainsValue(r)) ?? roles.FirstOrDefault() ?? "Employee";
        var clientRole = clientRoleOverride ?? WmsMapper.ToClientRole(identityRole);
        var roleLabel = roleLabelOverride ?? WmsMapper.ToClientRoleLabel(identityRole);

        lookups.EmployeeByUserId.TryGetValue(user.Id, out var employee);
        employee ??= lookups.EmployeeByCode.GetValueOrDefault(user.EmployeeCode);

        return new MemberDto(
            user.Id.ToString(), user.EmployeeCode, user.Name, clientRole, roleLabel,
            user.Email ?? "", employee?.Phone ?? user.PhoneNumber ?? "",
            user.IsActive ? "active" : "inactive",
            employee?.JoinDate.ToString("dd/MM/yyyy") ?? WmsMapper.FormatDate(user.CreatedAt));
    }
}
