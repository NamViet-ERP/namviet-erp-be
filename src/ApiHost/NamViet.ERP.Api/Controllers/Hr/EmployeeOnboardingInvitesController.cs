using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/hr/onboarding-invites")]
[Authorize]
public class EmployeeOnboardingInvitesController(
    HrDbContext db,
    IEmployeeOnboardingInviteService invites,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [RequirePermission("hrm.members.settings.view")]
    public async Task<ActionResult<IReadOnlyList<EmployeeOnboardingInviteDto>>> List(CancellationToken ct)
    {
        var rows = await db.EmployeeOnboardingInvites.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .Select(i => ToDto(i, null))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<ActionResult<EmployeeOnboardingInviteDto>> Create(
        [FromBody] CreateEmployeeOnboardingInviteRequest request,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expiresAt = request.ExpiresAt?.UtcDateTime ?? now.AddDays(7);
        if (expiresAt <= now || expiresAt > now.AddDays(30))
            return BadRequest("Thời hạn lời mời phải trong tương lai và không quá 30 ngày.");

        var (invite, rawToken) = await invites.CreateAsync(currentUser.Id, expiresAt, ct);
        return Ok(ToDto(invite, rawToken));
    }

    [HttpPost("{id:int}/revoke")]
    [RequirePermission("hrm.members.settings.update")]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        var invite = await db.EmployeeOnboardingInvites.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invite is null) return NotFound();
        if (invite.ConsumedAt is not null) return Conflict("Lời mời đã được sử dụng.");
        if (invite.RevokedAt is null)
        {
            invite.RevokedAt = DateTime.UtcNow;
            invite.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [HttpGet("validate")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicRead)]
    public async Task<ActionResult<EmployeeOnboardingInviteValidationDto>> Validate(
        [FromQuery] string token,
        CancellationToken ct)
    {
        var invite = await invites.FindValidAsync(token, ct);
        return Ok(new EmployeeOnboardingInviteValidationDto(invite is not null, invite?.ExpiresAt));
    }

    private static EmployeeOnboardingInviteDto ToDto(
        EmployeeOnboardingInvite invite,
        string? token) =>
        new(invite.Id, invite.ExpiresAt, invite.ConsumedAt, invite.RevokedAt,
            invite.CreatedByUserId, invite.ConsumedEmployeeId, token);
}
