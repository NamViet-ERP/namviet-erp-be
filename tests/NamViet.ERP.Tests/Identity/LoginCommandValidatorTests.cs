using FluentValidation.TestHelper;
using NamViet.ERP.Modules.Identity.Application.Commands;
using NamViet.ERP.Modules.Identity.Application.Validators;

namespace NamViet.ERP.Tests.Identity;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Should_fail_when_email_empty()
    {
        var result = _validator.TestValidate(new LoginCommand("", "password"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_fail_when_password_empty()
    {
        var result = _validator.TestValidate(new LoginCommand("user@namviet.vn", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_pass_with_valid_credentials()
    {
        var result = _validator.TestValidate(new LoginCommand("user@namviet.vn", "Secret123"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void Should_fail_when_refresh_token_empty()
    {
        var result = _validator.TestValidate(new RefreshTokenCommand(""));
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Should_pass_with_non_empty_token()
    {
        var result = _validator.TestValidate(new RefreshTokenCommand("valid.refresh.token"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Should_fail_weak_password()
    {
        var cmd = new RegisterCommand("NV-0001", "Test User", "test@namviet.vn", "weak", null, "Employee");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_pass_strong_password()
    {
        var cmd = new RegisterCommand("NV-0001", "Test User", "test@namviet.vn", "Strong1Pass", "HR", "Employee");
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
