using System.Data.Common;
using ExamFlow.Api.Features.Auth;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ExamFlow.Api.Infrastructure.Data;

/// <summary>
/// Startup SQL work (identity seed, then job re-queue and battle restore) waits until the app first opens a database
/// connection for real work. A cold start caused by an anonymous visitor or a bot then never resumes a paused
/// serverless database. The seed runs once, before that first connection is used, so no request sees missing roles.
/// </summary>
public sealed class DatabaseStartup(IServiceProvider services, IConfiguration configuration, TimeProvider clock, ILogger<DatabaseStartup> logger)
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
    private static readonly AsyncLocal<bool> Seeding = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;

    /// <summary>When this process started; rows created later were queued by this process itself.</summary>
    public DateTimeOffset StartedAt { get; } = clock.GetUtcNow();

    /// <summary>Completes after the first successful seed; background startup work awaits it.</summary>
    public Task Ready => _ready.Task;

    public async ValueTask EnsureSeededAsync(CancellationToken ct)
    {
        if (_ready.Task.IsCompleted || Seeding.Value || clock.GetUtcNow() < _retryAt) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_ready.Task.IsCompleted || clock.GetUtcNow() < _retryAt) return;
            Seeding.Value = true; // the seed's own connections pass straight through
            if (await IdentitySeeder.SeedIdentityAsync(services, configuration, logger)) _ready.TrySetResult();
            // Database unreachable: the triggering request fails on its own; try again a little later.
            else _retryAt = clock.GetUtcNow() + RetryDelay;
        }
        finally { _gate.Release(); }
    }
}

/// <summary>Runs <see cref="DatabaseStartup"/> before the first connection the app opens.</summary>
public sealed class DatabaseStartupInterceptor(DatabaseStartup startup) : DbConnectionInterceptor
{
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        await startup.EnsureSeededAsync(cancellationToken);
        return result;
    }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        startup.EnsureSeededAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return result;
    }
}
