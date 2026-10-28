using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.Modules.Identity.Infrastructure.Services;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Tests.Identity;

public class ActiveAccountTokenValidatorTests
{
    [Theory]
    [InlineData(AccountLinkStatus.Active, false)]
    [InlineData(AccountLinkStatus.Locked, true)]
    [InlineData(AccountLinkStatus.None, true)]
    public async Task Existing_token_requires_an_active_account(AccountLinkStatus status, bool fails)
    {
        using var services = new ServiceCollection()
            .AddSingleton<IAccountStatusProvider>(new AccountStatusStub(status)).BuildServiceProvider();
        var context = new TokenValidatedContext(new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())]))
        };
        await ActiveAccountTokenValidator.ValidateAsync(context);
        Assert.Equal(fails, context.Result?.Failure is not null);
    }

    private sealed class AccountStatusStub(AccountLinkStatus status) : IAccountStatusProvider
    {
        public Task<AccountLinkStatus> GetStatusByUserIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(status);
        public Task<Dictionary<Guid, AccountLinkStatus>> GetStatusByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default) =>
            Task.FromResult(userIds.ToDictionary(id => id, _ => status));
    }
}
