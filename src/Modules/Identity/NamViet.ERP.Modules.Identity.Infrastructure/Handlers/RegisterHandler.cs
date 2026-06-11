using MediatR;
using Microsoft.AspNetCore.Identity;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Application.DTOs;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Handlers;

public class RegisterHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> _roleManager)
    : IRequestHandler<RegisterCommand, UserDto>
{
    public async Task<UserDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
            throw new InvalidOperationException($"Email '{request.Email}' đã được sử dụng.");

        var roleExists = await _roleManager.RoleExistsAsync(request.Role);
        var role = roleExists ? request.Role : "Employee";

        var user = new ApplicationUser
        {
            EmployeeCode = request.EmployeeCode,
            Name = request.Name,
            Email = request.Email,
            UserName = request.Email,
            Department = request.Department,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Tạo tài khoản thất bại: {errors}");
        }

        await userManager.AddToRoleAsync(user, role);
        var roles = await userManager.GetRolesAsync(user);

        return new UserDto(user.Id, user.EmployeeCode, user.Name, user.Email!, user.Department, user.AvatarUrl, [.. roles]);
    }
}
