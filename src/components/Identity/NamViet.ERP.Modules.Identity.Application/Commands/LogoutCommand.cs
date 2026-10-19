using MediatR;

namespace NamViet.ERP.Modules.Identity.Application.Commands;

public record LogoutCommand(string? RefreshToken) : IRequest;
