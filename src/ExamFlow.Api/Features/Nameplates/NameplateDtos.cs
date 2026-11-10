using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Nameplates;

public sealed record NameplateOptionDto(string Id, string Name, bool Restricted, bool Available, bool Granted);
public sealed record NameplateStateDto(string? Selected, bool IsAdmin, NameplateOptionDto[] Options);
public sealed class SelectNameplateRequest
{
    [MaxLength(4)]
    public string? Id { get; init; }
}
public sealed class NameplateGrantRequest
{
    [Required]
    public bool? Granted { get; init; }
}
