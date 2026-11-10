using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Auth;

public class AppUser : IdentityUser<Guid>
{
    [MaxLength(4)]
    public string? Nameplate { get; set; }
    public int NameplateGrants { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    [MaxLength(160)]
    public string? Bio { get; set; }
    [MaxLength(24)]
    public string Nickname { get; set; } = string.Empty;
    [MaxLength(24)]
    public string NormalizedNickname { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? AvatarFileName { get; set; }
    [MaxLength(32)]
    public string? AvatarContentType { get; set; }
}
