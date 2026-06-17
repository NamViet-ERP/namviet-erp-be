using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Auth;
using NamViet.ERP.Api.Application.Mappers.Hr;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using RegisterRequest = NamViet.ERP.Modules.Identity.Application.DTOs.RegisterRequest;
namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IMediator mediator, UserManager<ApplicationUser> userManager, HrDbContext hrDb) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        LoginWithUsernameAsync(request, ct);

    private async Task<IActionResult> LoginWithUsernameAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(u =>
                (u.UserName == request.Username || u.Email == request.Username) && u.DeletedAt == null, ct);
        if (user is null) return Unauthorized("Tài khoản không tồn tại.");
        if (!user.IsActive) return Unauthorized("Tài khoản của bạn đã bị khóa hoặc chưa được kích hoạt.");

        var auth = await mediator.Send(new LoginCommand(user.Email!, request.Password, request.RememberMe), ct);
        var roles = await userManager.GetRolesAsync(user);
        var feRole = WmsMapper.ToClientRole(roles.FirstOrDefault() ?? "Employee");
        var emp = await hrDb.Employees.FirstOrDefaultAsync(
            e => e.UserId == user.Id || e.Code == user.EmployeeCode, ct);
        var initials = string.Concat(user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(w => w[0])).ToUpper();
        var empStatus = emp is null ? null : HrEmployeeMapper.ToClientStatus(emp.Status);

        return Ok(new LoginResponse(auth.AccessToken, auth.RefreshToken,
            new AuthUserDto(user.Id.ToString(), user.UserName ?? user.Email ?? "",
                feRole, user.Name, initials, WmsMapper.ToClientRoleLabel(roles.FirstOrDefault() ?? "Employee"),
                user.Email ?? "", emp?.Id.ToString(), empStatus)));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            var auth = await mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct);
            return Ok(new TokenPairResponse(auth.AccessToken, auth.RefreshToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RegisterCommand(request.EmployeeCode, request.Name, request.Email,
                request.Password, request.Department, request.Role), ct);
        return CreatedAtAction(nameof(Register), result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = HttpContext.User;
        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userId, out var guid))
        {
            var appUser = await userManager.Users.FirstOrDefaultAsync(u => u.Id == guid, ct);
            if (appUser is not null)
            {
                var roles = await userManager.GetRolesAsync(appUser);
                var feRole = WmsMapper.ToClientRole(roles.FirstOrDefault() ?? "Employee");
                var initials = string.Concat(appUser.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2).Select(w => w[0])).ToUpper();
                return Ok(new AuthUserDto(appUser.Id.ToString(), appUser.UserName ?? "",
                    feRole, appUser.Name, initials, WmsMapper.ToClientRoleLabel(roles.FirstOrDefault() ?? "Employee"),
                    appUser.Email ?? "", null, null));
            }
        }
        return Ok(new
        {
            Id = user.FindFirst("sub")?.Value,
            Email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            Name = user.FindFirst("name")?.Value,
            EmployeeCode = user.FindFirst("employee_code")?.Value,
            Roles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)
        });
    }
}
