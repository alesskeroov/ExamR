using System.ComponentModel.DataAnnotations;

namespace ExamFlow.Api.Features.Feedback;

public sealed class FeedbackRequest
{
    public Guid RequestId { get; init; }
    [Required, RegularExpression("^(problem|idea)$", ErrorMessage = "Müraciətin növü yanlışdır.")]
    public string Kind { get; init; } = "";
    [Required(ErrorMessage = "Müraciətin mətnini yazın."), StringLength(1500, MinimumLength = 5, ErrorMessage = "Mətn 5–1500 simvol olmalıdır.")]
    public string Text { get; init; } = "";
    [MaxLength(255)]
    public string? DocumentName { get; init; }
    [Required, MaxLength(256), RegularExpression(@"^/[a-zA-Z0-9/_-]*$", ErrorMessage = "Səhifənin ünvanı yanlışdır.")]
    public string PagePath { get; init; } = "/guide";
}

public sealed record FeedbackAccepted(Guid Id);

public sealed class FeedbackOptions
{
    [Required, EmailAddress]
    public string Recipient { get; init; } = "examr.contact@gmail.com";
}
