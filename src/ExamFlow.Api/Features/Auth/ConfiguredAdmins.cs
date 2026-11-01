using System.Net;
using System.Text;
using ExamFlow.Api.Infrastructure.Email;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace ExamFlow.Api.Features.Auth;

public static class ConfiguredAdmins
{
    public static string[] Emails(IConfiguration configuration) => configuration.GetSection("AdminAccess:Emails").Get<string[]>() ?? [];
    public static bool IsConfigured(IConfiguration configuration, AppUser user) =>
        Emails(configuration).Any(email => string.Equals(email.Trim(), user.Email, StringComparison.OrdinalIgnoreCase));

    public static async Task EnsureAsync(UserManager<AppUser> users, IConfiguration configuration, AppUser user)
    {
        if (!IsConfigured(configuration, user)) return;
        var trusted = configuration.GetSection("AdminAccess:ExistingUserIds").Get<string[]>() ?? [];
        // A newly registered email alone is not proof of ownership of an administrator address.
        if (!user.EmailConfirmed && !trusted.Contains(user.Id.ToString(), StringComparer.OrdinalIgnoreCase)) return;
        if (await users.IsInRoleAsync(user, Roles.Admin)) return;
        var result = await users.AddToRoleAsync(user, Roles.Admin);
        if (!result.Succeeded && !await users.IsInRoleAsync(user, Roles.Admin))
            throw new InvalidOperationException("Configured administrator role could not be assigned.");
        await users.UpdateSecurityStampAsync(user);
    }

    public static async Task SendConfirmationAsync(UserManager<AppUser> users, IConfiguration configuration, EmailQueue email, AppUser user)
    {
        if (!IsConfigured(configuration, user) || user.EmailConfirmed || await users.IsInRoleAsync(user, Roles.Admin)) return;
        var frontend = configuration["App:FrontendBaseUrl"];
        if (string.IsNullOrWhiteSpace(frontend)) throw new InvalidOperationException("App:FrontendBaseUrl is required for administrator verification.");
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{frontend.TrimEnd('/')}/api/auth/confirm-admin?userId={user.Id}&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}";
        email.Enqueue(new EmailMessage(user.Email!, "ExamR — admin emailinin təsdiqi",
            $"""<h2>Admin emailini təsdiqlə</h2><p>Bu ünvan serverdə idarəçi kimi təyin edilib. Rolun aktivləşməsi üçün emailə sahib olduğunuzu təsdiqləyin.</p><p><a href="{WebUtility.HtmlEncode(link)}">Emaili təsdiqlə</a></p>"""));
    }
}
