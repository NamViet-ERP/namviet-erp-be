using MediatR;
using NamViet.ERP.Modules.Identity.Application.DTOs;

namespace NamViet.ERP.Modules.Identity.Application.Commands;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;
