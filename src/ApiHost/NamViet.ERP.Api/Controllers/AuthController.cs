using MediatR;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.RateLimiting;

using Microsoft.EntityFrameworkCore;

using NamViet.ERP.Api.Application.Dtos.Auth;

using NamViet.ERP.Api.Application.Mappers.Wms;

using NamViet.ERP.Api.Infrastructure.RateLimiting;

using NamViet.ERP.Modules.Identity.Application.Abstractions;

using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Identity.Application.Commands;

using NamViet.ERP.Modules.Identity.Domain.Entities;

using NamViet.ERP.SharedKernel.Interfaces;

using RegisterRequest = NamViet.ERP.Modules.Identity.Application.DTOs.RegisterRequest;



namespace NamViet.ERP.Api.Controllers;



[ApiController]

[Route("api/auth")]

public class AuthController(

    IMediator mediator,

    UserManager<ApplicationUser> userManager,

    IPermissionService permissionService,

    IEmployeeInfoProvider employeeInfo) : ControllerBase

{

    [HttpPost("login")]

    [AllowAnonymous]

    [EnableRateLimiting(RateLimitPolicies.Auth)]

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

        var authUser = await BuildAuthUserDtoAsync(user, ct);



        return Ok(new LoginResponse(auth.AccessToken, auth.RefreshToken, authUser));

    }



    [HttpPost("refresh")]

    [AllowAnonymous]

    [EnableRateLimiting(RateLimitPolicies.Auth)]

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
    [RequirePermission("admin.users.create")]
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

        var userId = User.FindFirst("sub")?.Value

            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userId, out var guid))

            return Unauthorized();



        var appUser = await userManager.Users.FirstOrDefaultAsync(u => u.Id == guid, ct);

        if (appUser is null)

            return Unauthorized();



        return Ok(await BuildAuthUserDtoAsync(appUser, ct));

    }



    private async Task<AuthUserDto> BuildAuthUserDtoAsync(ApplicationUser user, CancellationToken ct)

    {

        var roles = await userManager.GetRolesAsync(user);

        var identityRole = roles.FirstOrDefault() ?? "Employee";

        var clientRole = WmsMapper.ToClientRole(identityRole);

        var linked = await employeeInfo.GetByUserAsync(user.Id, user.EmployeeCode, ct);

        var initials = string.Concat(user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)

            .Take(2).Select(w => w[0])).ToUpper();

        var permissions = await permissionService.GetPermissionsForUserAsync(user, ct);



        return new AuthUserDto(

            user.Id.ToString(),

            user.UserName ?? user.Email ?? "",

            clientRole,

            user.Name,

            initials,

            WmsMapper.ToClientRoleLabel(identityRole),

            user.Email ?? "",

            linked?.EmployeeId.ToString(),

            linked?.ClientStatus,

            permissions);

    }

}


