using Microsoft.AspNetCore.Identity;

namespace ExamFlow.Api.Features.Auth;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
