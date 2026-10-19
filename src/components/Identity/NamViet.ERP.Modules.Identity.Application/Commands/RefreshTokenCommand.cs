using MediatR;
using NamViet.ERP.Modules.Identity.Application.DTOs;

namespace NamViet.ERP.Modules.Identity.Application.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;
