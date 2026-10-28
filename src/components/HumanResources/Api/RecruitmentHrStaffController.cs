using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NamViet.ERP.Api.Infrastructure.RateLimiting;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/hr-staff")]
public class RecruitmentHrStaffController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicRead)]
    public async Task<ActionResult<List<HrStaffPublicDto>>> List(CancellationToken ct)
    {
        var users = await userManager.GetUsersInRoleAsync("HR");
        var items = users
            .Where(u => u.IsActive && u.DeletedAt == null && !string.IsNullOrWhiteSpace(u.Name))
            .OrderBy(u => u.Name)
            .Select(u => new HrStaffPublicDto(u.Name!))
            .ToList();
        return Ok(items);
    }
}
