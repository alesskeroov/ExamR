namespace ExamFlow.Api.Features.Usage;

// Structured Tutor generators have multiple return types; preserve the complete quota response across them.
public sealed class UsageLimitException(UsageCheck check) : Exception(UsageQuota.LimitMessage(check.Kind, check.Limit, check.ResetsAt))
{
    public UsageCheck Check { get; } = check;
}

public sealed class UsageLimitFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UsageLimitException ex) { return ex.Check.Problem(); }
    }
}
