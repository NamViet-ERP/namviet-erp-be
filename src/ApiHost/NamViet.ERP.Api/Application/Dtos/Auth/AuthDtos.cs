namespace NamViet.ERP.Api.Application.Dtos.Auth;

public record LoginRequest(string Username, string Password, bool RememberMe = false);

public record RefreshTokenRequest(string RefreshToken);

public record TokenPairResponse(string Token, string RefreshToken);

public record AuthUserDto(
    string Id,
    string Username,
    string Role,
    string Name,
    string Initials,
    string RoleLabel,
    string Email,
    string? EmployeeId,
    string? EmployeeStatus);

public record LoginResponse(string Token, string RefreshToken, AuthUserDto User);
