using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Auth;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    [MaxLength(24)]
    public string Nickname { get; set; } = string.Empty;
    [MaxLength(24)]
    public string NormalizedNickname { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? AvatarFileName { get; set; }
    [MaxLength(32)]
    public string? AvatarContentType { get; set; }
}
