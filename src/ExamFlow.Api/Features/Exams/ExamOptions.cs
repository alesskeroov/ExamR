namespace ExamFlow.Api.Features.Exams;

public sealed class ExamOptions
{
    public const string Section = "Exam";

    /// <summary>Upper bound enforced by a database check constraint.</summary>
    public const int HardMaxQuestions = 200;

    public int MaxQuestions { get; set; } = HardMaxQuestions;
    /// <summary>Time limit when the user does not choose one, whatever the question count.</summary>
    public int DefaultDurationMinutes { get; set; } = 30;

    /// <summary>Tolerance for answers that were sent just before the deadline.</summary>
    public int AnswerGraceSeconds { get; set; } = 5;

    /// <summary>Upper bound of a time limit chosen by the user (minutes).</summary>
    public const int MaxDurationMinutes = 600;

    public TimeSpan DefaultDuration => TimeSpan.FromMinutes(DefaultDurationMinutes);
}
