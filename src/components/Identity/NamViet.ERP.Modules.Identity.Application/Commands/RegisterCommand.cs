using MediatR;
using NamViet.ERP.Modules.Identity.Application.DTOs;

namespace NamViet.ERP.Modules.Identity.Application.Commands;

public record RegisterCommand(
    string EmployeeCode,
    string Name,
    string Email,
    string Password,
    string? Department,
    string Role) : IRequest<UserDto>;
