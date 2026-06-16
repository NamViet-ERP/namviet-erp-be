using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Api.Controllers.Hr;

[ApiController]
[Route("api/recruitment/hr-staff")]
public class RecruitmentHrStaffController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<FeHrStaffPublicDto>>> List(CancellationToken ct)
    {
        var users = await userManager.GetUsersInRoleAsync("HR");
        var items = users
            .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Name))
            .OrderBy(u => u.Name)
            .Select(u => new FeHrStaffPublicDto(u.Id.ToString(), u.Name!))
            .ToList();
        return Ok(items);
    }
}
