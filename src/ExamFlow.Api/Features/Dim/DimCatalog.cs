namespace ExamFlow.Api.Features.Dim;

/// <summary>Buraxılış = 11th-grade graduation exam (300 points); Blok = admission stage II (400 points).</summary>
public enum DimTrack
{
    Buraxilis,
    Blok,
}

public enum DimTestKind
{
    /// <summary>All subjects of the track (Buraxılış) or of one specialty group (Blok).</summary>
    Full,

    /// <summary>A single subject.</summary>
    Subject,
}

/// <summary>
/// Official task structure of one subject: closed (A–E), open coded and open written tasks. The subject's 100-point
/// scale is <c>100 / (Closed + Coded + 2 × Written)</c> per point; Blok closed tasks lose a quarter per wrong answer.
/// </summary>
public sealed record DimSubjectSpec(string Code, string Name, int Closed, int Coded, int Written, bool WrongPenalty)
{
    public int Denominator => Closed + Coded + 2 * Written;
    public int TaskCount => Closed + Coded + Written;
}

public sealed record DimGroupSubject(string Code, decimal Coefficient);

/// <summary>A Blok specialty group or subgroup (I-RK, I-Rİ, II, III-DT, III-TC, IV).</summary>
public sealed record DimGroup(string Code, string Name, IReadOnlyList<DimGroupSubject> Subjects);

/// <summary>
/// DİM exam structure (Azerbaijani sector), 2025–2026 rules as published by DİM and summarised by public score
/// calculators. Kept as data so a rule change is a one-line edit; verify against the official DİM document.
/// </summary>
public static class DimCatalog
{
    public const int SubjectMaxPoints = 100;

    private static readonly Dictionary<string, string> SubjectNames = new()
    {
        ["az"] = "Azərbaycan dili",
        ["riy"] = "Riyaziyyat",
        ["xdil"] = "Xarici dil (İngilis dili)",
        ["fiz"] = "Fizika",
        ["kim"] = "Kimya",
        ["inf"] = "İnformatika",
        ["bio"] = "Biologiya",
        ["tar"] = "Tarix",
        ["cog"] = "Coğrafiya",
        ["edb"] = "Ədəbiyyat",
    };

    /// <summary>XI grade graduation exam: three subjects, 100 points each, no penalty for wrong answers.</summary>
    public static readonly IReadOnlyList<DimSubjectSpec> Buraxilis =
    [
        new("az", SubjectNames["az"], Closed: 20, Coded: 0, Written: 10, WrongPenalty: false),
        new("riy", SubjectNames["riy"], Closed: 13, Coded: 5, Written: 7, WrongPenalty: false),
        new("xdil", SubjectNames["xdil"], Closed: 23, Coded: 0, Written: 7, WrongPenalty: false),
    ];

    public static readonly IReadOnlyList<DimGroup> Groups =
    [
        new("I-RK", "I qrup (RK)", [new("riy", 1.5m), new("fiz", 1.5m), new("kim", 1m)]),
        new("I-RI", "I qrup (Rİ)", [new("riy", 1.5m), new("fiz", 1.5m), new("inf", 1m)]),
        new("II", "II qrup", [new("riy", 1.5m), new("cog", 1.5m), new("tar", 1m)]),
        new("III-DT", "III qrup (DT)", [new("az", 1.5m), new("tar", 1.5m), new("edb", 1m)]),
        new("III-TC", "III qrup (TC)", [new("az", 1.5m), new("tar", 1.5m), new("cog", 1m)]),
        new("IV", "IV qrup", [new("bio", 1.5m), new("kim", 1.5m), new("fiz", 1m)]),
    ];

    /// <summary>Every Blok subject: 30 tasks (22 closed, 5 coded, 3 written), wrong closed answers cost ¼.</summary>
    public static DimSubjectSpec BlokSubject(string code) =>
        new(code, SubjectNames[code], Closed: 22, Coded: 5, Written: 3, WrongPenalty: true);

    public static IReadOnlyList<string> BlokSubjectCodes { get; } =
        Groups.SelectMany(g => g.Subjects.Select(s => s.Code)).Distinct().ToList();

    public static IReadOnlyList<string> SubjectCodes(DimTrack track) =>
        track == DimTrack.Buraxilis ? Buraxilis.Select(s => s.Code).ToList() : BlokSubjectCodes;

    public static bool IsSubjectOf(DimTrack track, string code) => SubjectCodes(track).Contains(code);

    public static DimGroup? Group(string? code) => Groups.FirstOrDefault(g => g.Code == code);

    public static DimSubjectSpec Spec(DimTrack track, string code) =>
        track == DimTrack.Buraxilis ? Buraxilis.First(s => s.Code == code) : BlokSubject(code);

    /// <summary>Weight of a subject in the total: Blok group coefficient (1.5 / 1); Buraxılış and single-subject tests use 1.</summary>
    public static decimal Coefficient(DimTrack track, string? groupCode, string subject) =>
        track == DimTrack.Blok && Group(groupCode)?.Subjects.FirstOrDefault(s => s.Code == subject) is { } weighted
            ? weighted.Coefficient
            : 1m;

    public static string SubjectName(string code) => SubjectNames.GetValueOrDefault(code, code);
}
