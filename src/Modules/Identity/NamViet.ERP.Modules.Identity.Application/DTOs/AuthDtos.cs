namespace NamViet.ERP.Modules.Identity.Application.DTOs;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(
    string EmployeeCode,
    string Name,
    string Email,
    string Password,
    string? Department,
    string Role = "employee");

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record RefreshTokenRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    UserDto User);

public record UserDto(
    Guid Id,
    string EmployeeCode,
    string Name,
    string Email,
    string? Department,
    string? AvatarUrl,
    IReadOnlyList<string> Roles);
