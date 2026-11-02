using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Tests;

public class AccountTokenTests
{
    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        using var manager = Manager();
        var user = new AppUser { Id = Guid.NewGuid(), SecurityStamp = "first" };
        var provider = Provider(TimeSpan.FromSeconds(-1));
        var token = await provider.GenerateAsync("ResetPassword", manager, user);
        Assert.False(await provider.ValidateAsync("ResetPassword", token, manager, user));
    }

    [Fact]
    public async Task TokenCannotBeReusedAfterStampRotationOrForAnotherPurpose()
    {
        using var manager = Manager();
        var user = new AppUser { Id = Guid.NewGuid(), SecurityStamp = "first" };
        var provider = Provider(TimeSpan.FromMinutes(30));
        var token = await provider.GenerateAsync("ExamR.DeleteAccount", manager, user);
        Assert.True(await provider.ValidateAsync("ExamR.DeleteAccount", token, manager, user));
        Assert.False(await provider.ValidateAsync("ResetPassword", token, manager, user));
        Assert.False(await provider.ValidateAsync("ExamR.DeleteAccount", token, manager, new AppUser { Id = Guid.NewGuid(), SecurityStamp = "first" }));
        user.SecurityStamp = "second";
        Assert.False(await provider.ValidateAsync("ExamR.DeleteAccount", token, manager, user));
    }

    [Fact]
    public async Task ConfirmationTokenRejectsExpiredAndTamperedValues()
    {
        using var manager = Manager();
        var user = new AppUser { Id = Guid.NewGuid(), SecurityStamp = "first" };
        var provider = new EmailConfirmationTokenProvider(new EphemeralDataProtectionProvider(),
            Options.Create(new EmailConfirmationTokenProviderOptions { TokenLifespan = TimeSpan.FromSeconds(-1) }),
            NullLogger<DataProtectorTokenProvider<AppUser>>.Instance);
        var token = await provider.GenerateAsync("EmailConfirmation", manager, user);
        Assert.False(await provider.ValidateAsync("EmailConfirmation", token, manager, user));
        Assert.False(await provider.ValidateAsync("EmailConfirmation", "invalid", manager, user));
    }

    private static DataProtectorTokenProvider<AppUser> Provider(TimeSpan lifetime) => new(new EphemeralDataProtectionProvider(),
        Options.Create(new DataProtectionTokenProviderOptions { TokenLifespan = lifetime }), NullLogger<DataProtectorTokenProvider<AppUser>>.Instance);
    private static UserManager<AppUser> Manager() => new(new StampStore(), Options.Create(new IdentityOptions()),
        new PasswordHasher<AppUser>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
        new ServiceCollection().BuildServiceProvider(), NullLogger<UserManager<AppUser>>.Instance);

    private sealed class StampStore : IUserSecurityStampStore<AppUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(AppUser user, string? name, CancellationToken ct) { user.UserName = name; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(AppUser user, string? name, CancellationToken ct) { user.NormalizedUserName = name; return Task.CompletedTask; }
        public Task SetSecurityStampAsync(AppUser user, string stamp, CancellationToken ct) { user.SecurityStamp = stamp; return Task.CompletedTask; }
        public Task<string?> GetSecurityStampAsync(AppUser user, CancellationToken ct) => Task.FromResult(user.SecurityStamp);
        public Task<IdentityResult> CreateAsync(AppUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<AppUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult<AppUser?>(null);
        public Task<AppUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult<AppUser?>(null);
    }
}
