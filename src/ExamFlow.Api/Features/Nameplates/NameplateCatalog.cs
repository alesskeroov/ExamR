namespace ExamFlow.Api.Features.Nameplates;

public sealed record NameplateDefinition(string Id, string Name, int Permission);

public static class NameplateCatalog
{
    public static readonly IReadOnlyList<NameplateDefinition> All =
    [
        new("np01", "Mavi ruh alovu", 0),
        new("np06", "Neon şəhər", 0),
        new("np08", "Noir yağış", 0),
        new("np09", "Futbol 10", 1),
        new("np11", "Ronin", 2),
        new("np12", "Əjdaha gözü", 4),
        new("np14", "Qaçan pişik", 8)
    ];

    public static NameplateDefinition? Find(string? id) => All.FirstOrDefault(x => x.Id == id);

    public static bool CanUse(string? id, bool isAdmin, int grants) => id is null ||
        Find(id) is { } item && (isAdmin || item.Permission == 0 || (grants & item.Permission) != 0);

    public static NameplateStateDto State(string? selected, bool isAdmin, int grants) => new(
        CanUse(selected, isAdmin, grants) ? selected : null,
        isAdmin,
        All.Select(x => new NameplateOptionDto(x.Id, x.Name, x.Permission != 0,
            CanUse(x.Id, isAdmin, grants), x.Permission != 0 && (grants & x.Permission) != 0)).ToArray());
}
