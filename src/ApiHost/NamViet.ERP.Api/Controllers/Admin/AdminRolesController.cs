using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Admin;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NamViet.ERP.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = "Admin")]
public class AdminRolesController(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminRoleDto>>> List(
        [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var roles = await roleManager.Roles.OrderBy(r => r.Name).ToListAsync(ct);
        IEnumerable<ApplicationRole> filtered = roles;

        if (activeOnly)
            filtered = filtered.Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            filtered = filtered.Where(r =>
                ContainsIgnoreCase(r.Description, q)
                || ContainsIgnoreCase(r.Name, q)
                || ContainsIgnoreCase(WmsMapper.ToClientRole(r.Name ?? ""), q));
        }

        return Ok(filtered.Select(ToAdminRoleDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AdminRoleDto>> Create([FromBody] CreateAdminRoleRequest request, CancellationToken ct)
    {
        var label = request.Label.Trim();
        if (string.IsNullOrEmpty(label))
            return BadRequest("Tên vai trò không được để trống.");

        var code = (request.Code?.Trim() ?? Slugify(label)).ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[a-z][a-z0-9_]*$"))
            return BadRequest("Mã vai trò chỉ gồm chữ thường, số và dấu gạch dưới.");

        var identityName = WmsMapper.ToIdentityRole(code);
        if (await roleManager.RoleExistsAsync(identityName))
            return Conflict("Mã vai trò đã tồn tại.");

        var existing = await roleManager.Roles
            .FirstOrDefaultAsync(r => r.Description != null && r.Description.ToLower() == label.ToLower(), ct);
        if (existing is not null)
            return Conflict("Tên vai trò đã tồn tại.");

        var role = new ApplicationRole
        {
            Name = identityName,
            Description = label,
            Module = "Custom",
            IsActive = true,
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(ToAdminRoleDto(role));
    }

    [HttpPatch("{code}/toggle")]
    public async Task<ActionResult<AdminRoleDto>> Toggle(string code, CancellationToken ct)
    {
        var role = await FindRoleByClientCodeAsync(code, ct);
        if (role is null) return NotFound();

        role.IsActive = !role.IsActive;
        await roleManager.UpdateAsync(role);
        return Ok(ToAdminRoleDto(role));
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        var role = await FindRoleByClientCodeAsync(code, ct);
        if (role is null) return NotFound();

        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
            return BadRequest("Không thể xóa vai trò đang được gán cho người dùng.");

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return NoContent();
    }

    private static bool ContainsIgnoreCase(string? value, string query)
        => !string.IsNullOrEmpty(value) &&
           value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task<ApplicationRole?> FindRoleByClientCodeAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToLowerInvariant();
        var roles = await roleManager.Roles.ToListAsync(ct);
        return roles.FirstOrDefault(r =>
            string.Equals(WmsMapper.ToClientRole(r.Name ?? ""), normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.Name, WmsMapper.ToIdentityRole(normalized), StringComparison.OrdinalIgnoreCase));
    }

    private static AdminRoleDto ToAdminRoleDto(ApplicationRole role) =>
        new(
            WmsMapper.ToClientRole(role.Name ?? ""),
            !string.IsNullOrWhiteSpace(role.Description)
                ? role.Description!
                : WmsMapper.ToClientRoleLabel(role.Name ?? ""),
            role.IsActive);

    private static string Slugify(string label)
    {
        var cleaned = new string(label.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        var slug = Regex.Replace(cleaned.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(slug) ? $"role_{DateTime.UtcNow.Ticks}" : slug;
    }
}
