using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public static class ActiveAccountTokenValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
        {
            context.Fail("Tài khoản không hợp lệ.");
            return;
        }

        var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountStatusProvider>();
        var status = await accounts.GetStatusByUserIdAsync(userId, context.HttpContext.RequestAborted);
        if (status != AccountLinkStatus.Active)
            context.Fail("Tài khoản đã bị khóa hoặc không còn tồn tại.");
    }
}
