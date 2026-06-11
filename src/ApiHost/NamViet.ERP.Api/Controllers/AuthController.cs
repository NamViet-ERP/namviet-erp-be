using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Application.DTOs;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(request.Email, request.Password), ct);
        return Ok(result);
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
    public IActionResult Me()
    {
        var user = HttpContext.User;
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
