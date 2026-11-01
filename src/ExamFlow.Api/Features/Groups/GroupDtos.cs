using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Groups;

public sealed record SaveGroupRequest(
    [property: Required(ErrorMessage = "Qrupun adını daxil edin."), StringLength(120, ErrorMessage = "Ad ən çox 120 simvol ola bilər.")] string Name,
    [property: Required(AllowEmptyStrings = true), StringLength(500, ErrorMessage = "Təsvir ən çox 500 simvol ola bilər.")] string Description);
public sealed record AddGroupMembersRequest(
    [property: Required(ErrorMessage = "İstifadəçi seçin."), MinLength(1, ErrorMessage = "İstifadəçi seçin."), MaxLength(100, ErrorMessage = "Bir dəfəyə ən çox 100 istifadəçi əlavə edin.")] Guid[] UserIds);
public sealed record GroupDto(Guid Id, string Name, string Description, DateTimeOffset CreatedAt, int MemberCount);
public sealed record GroupDocumentDto(Guid Id, string FileName, int QuestionCount);
public sealed record GroupMemberDto(Guid Id, string Nickname, string? AvatarUrl);
public sealed record GroupSearchUserDto(Guid Id, string Nickname, string? AvatarUrl, bool IsMember);
