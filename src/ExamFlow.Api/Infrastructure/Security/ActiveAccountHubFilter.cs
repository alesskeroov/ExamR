using ExamFlow.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace ExamFlow.Api.Infrastructure.Security;

/// <summary>Existing hub connections must not keep invoking methods after account or password revocation.</summary>
public sealed class ActiveAccountHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var users = context.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = context.Context.User is null ? null : await users.GetUserAsync(context.Context.User);
        if (user is null || user.IsDeleted || user.SecurityStamp != context.Context.User?.FindFirst(JwtClaims.SecurityStamp)?.Value)
        {
            context.Context.Abort();
            throw new HubException("Sessiyanın müddəti bitib. Yenidən daxil olun.");
        }
        return await next(context);
    }
}
