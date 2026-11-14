using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Courses;

public sealed class CourseRequest
{
    [Required(ErrorMessage = "Fənnin adını yazın.")]
    [StringLength(StudyCourse.MaxName, MinimumLength = 2, ErrorMessage = "Fənnin adı 2–100 simvol olmalıdır.")]
    public string? Name { get; init; }

    /// <summary>Day of the exam; null = not known yet.</summary>
    public DateOnly? ExamDate { get; init; }
}

public sealed class CourseDocumentsRequest
{
    [Required, MaxLength(StudyCourse.MaxMaterials, ErrorMessage = "Fənnə ən çox 10 mühazirə faylı əlavə etmək olar.")]
    public List<Guid>? Materials { get; init; }

    [Required, MaxLength(StudyCourse.MaxBanks, ErrorMessage = "Fənnə ən çox 3 sual bankı əlavə etmək olar.")]
    public List<Guid>? Banks { get; init; }
}

public sealed record CourseListItem(Guid Id, string Name, DateOnly? ExamDate, int? DaysLeft, int MaterialCount, int BankCount);

/// <summary>A file of the course. Material: Format/Status of its guide index and the student's guide entry (ChatId). Bank: its question count.</summary>
public sealed record CourseDocumentDto(Guid DocumentId, string Name, string Role, string Format, string? Status, Guid? ChatId, int? QuestionCount);
public sealed record CourseDetail(Guid Id, string Name, DateOnly? ExamDate, int? DaysLeft, IReadOnlyList<CourseDocumentDto> Documents);

/// <summary>A file the student may add: lecture material for the guide or a question bank.</summary>
public sealed record CourseCandidate(Guid DocumentId, string Name, string Format, string Role, DateTimeOffset UploadedAt);

public sealed record MaterialProgress(Guid DocumentId, string Name, string Format, Guid? ChatId, bool Ready, int Cards, int Mastered, int Weak);
public sealed record BankProgress(Guid DocumentId, string Name, int Questions, int Answered, int Correct, int Wrong);

/// <summary>A part of a lecture that the student's wrong exam answers point to.</summary>
public sealed record WeakSpot(Guid MaterialDocumentId, string MaterialName, string Format, Guid? ChatId, string? Section, int? PageFrom, int? PageTo,
    int ChunkOrdinal, int WrongAnswers);

/// <summary>
/// One of today's tasks. Kind: Mistakes (re-test wrong answers), Read (a weak lecture part), Cards (study cards), Test (new
/// bank questions), Done (nothing left). DocumentId is the bank (Mistakes/Test) or the material (Read/Cards).
/// </summary>
public sealed record CourseTask(string Kind, string Title, string Detail, Guid? DocumentId, Guid? ChatId, int? Count, int? ChunkOrdinal);

/// <summary>
/// The course's preparation panel. Readiness = share of mastered cards and of bank questions last answered correctly
/// (each half when both exist). DaysLeft is null without an exam date or after it.
/// </summary>
public sealed record CoursePlan(int? DaysLeft, int Readiness, IReadOnlyList<MaterialProgress> Materials, IReadOnlyList<BankProgress> Banks,
    IReadOnlyList<WeakSpot> WeakSpots, IReadOnlyList<CourseTask> Today);

/// <summary>Where in the course's lectures a question of a finished exam is explained. Position = the question's place in the exam (as in the result).</summary>
public sealed record ExamQuestionLink(int Position, Guid MaterialDocumentId, string MaterialName, string Format, Guid? ChatId, string? Section,
    int? PageFrom, int? PageTo, int ChunkOrdinal, string Excerpt);
