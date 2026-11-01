using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Study;

public sealed record StartPracticeRequest(Guid DocumentId,
    [property: Required(ErrorMessage = "Öyrənmə rejimini seçin."), RegularExpression("^(open|mcq)$", ErrorMessage = "Öyrənmə rejimi düzgün deyil.")] string Mode,
    [property: Range(1, 30, ErrorMessage = "Sual sayı 1–30 aralığında olmalıdır.")] int Count = 10);
public sealed record PracticeActionRequest(
    [property: Required(ErrorMessage = "Əməliyyat tələb olunur."), RegularExpression("^(next|hint|explain|reveal|answer|respond)$", ErrorMessage = "Əməliyyat düzgün deyil.")] string Action,
    Guid QuestionId, int? OptionIndex = null,
    [property: StringLength(16000, ErrorMessage = "Cavab 16000 simvoldan uzun ola bilməz.")] string? Answer = null);
public sealed record PracticeQuestion(Guid Id, int Number, string Text, string[] Options);
public sealed record PracticeFeedback(Guid QuestionId, string Topic, bool Correct, string Explanation,
    int SelectedIndex, int CorrectIndex);
public sealed record PracticeResult(int Total, int Correct, int Incorrect, decimal Percent,
    string[] WeakTopics, PracticeFeedback[] Mistakes);
public sealed record PracticeView(Guid Id, string Mode, string Difficulty, int Position, int Total,
    PracticeQuestion? Question, PracticeFeedback? Feedback, string? Help, bool Finished, PracticeResult? Result, string? Answer = null, PracticeEvaluation? Evaluation = null);
public sealed record PracticeEvaluation(int Score, string Verdict, string[] MissingConcepts);
public sealed record PracticeSummary(Guid Id, string Mode, string Difficulty, DateTimeOffset CreatedAt,
    bool Finished, int Answered, int Total, PracticeResult? Result);

// Stored as JSON in the existing local StudyAiTurns records; never returned directly.
public sealed class PracticeState
{
    public string Mode { get; set; } = "open";
    public string Difficulty { get; set; } = "medium";
    public Guid[] QuestionIds { get; set; } = [];
    public int Index { get; set; }
    public Guid? AttemptId { get; set; }
    public List<PracticeFeedback> Answers { get; set; } = [];
    public string? Help { get; set; }
    public string? Answer { get; set; }
    public PracticeEvaluation? Evaluation { get; set; }
    public List<Guid> AttemptIds { get; set; } = [];
}
