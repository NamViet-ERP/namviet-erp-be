namespace NamViet.ERP.Api.Application.Dtos.Auth;

public record LoginRequest(string Username, string Password, bool RememberMe = false);

/// <summary>Optional body refresh token — preferred transport is HttpOnly cookie.</summary>
public record RefreshTokenRequest(string? RefreshToken = null);

/// <summary>Refresh success — new access token in body; refresh rotates via HttpOnly cookie.</summary>
public record TokenPairResponse(string AccessToken);

public record AuthUserDto(
    string Id,
    string Username,
    string Role,
    string Name,
    string Initials,
    string RoleLabel,
    string Email,
    string? EmployeeId,
    string? EmployeeStatus,
    IReadOnlyList<string> Permissions);

public record LoginResponse(AuthUserDto User, string? AccessToken = null);
