using System.ComponentModel.DataAnnotations;
using ExamFlow.Api.Features.Feedback;

namespace ExamFlow.Api.Tests;

public class FeedbackTests
{
    [Fact]
    public void Mail_escapes_user_input_and_keeps_user_text_out_of_headers()
    {
        var message = new FeedbackMessage
        {
            Id = Guid.NewGuid(), Kind = "problem", SenderName = "<script>alert(1)</script>",
            SenderEmail = "student@example.test", Text = "<img src=x onerror=alert(1)>\nBcc: attacker@example.test",
            DocumentName = "<b>Exam</b>.pdf", PagePath = "/guide", CreatedAt = DateTimeOffset.UtcNow,
        };
        var mail = FeedbackEmail.Build(message, "examr.contact@gmail.com");
        Assert.Equal("examr.contact@gmail.com", mail.To);
        Assert.DoesNotContain("<script>", mail.HtmlBody);
        Assert.DoesNotContain("<img", mail.HtmlBody);
        Assert.Contains("&lt;img", mail.HtmlBody);
        Assert.Contains(message.Text, mail.TextBody);
        Assert.Contains(message.SenderEmail, mail.TextBody);
        Assert.Contains(message.Id.ToString(), mail.Subject);
        Assert.DoesNotContain("Bcc", mail.Subject);
        Assert.DoesNotContain('\n', mail.Subject);
    }

    [Theory]
    [InlineData("", "problem", "/guide")]
    [InlineData("abc", "problem", "/guide")]
    [InlineData("normal message", "spam", "/guide")]
    [InlineData("normal message", "idea", "https://attacker.test")]
    [InlineData("normal message", "idea", "/guide?token=private")]
    public void Invalid_feedback_is_rejected(string text, string kind, string path)
    {
        var request = new FeedbackRequest { RequestId = Guid.NewGuid(), Text = text, Kind = kind, PagePath = path };
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public void Feedback_payload_limits_match_the_database_and_valid_requests_pass()
    {
        var valid = new FeedbackRequest { RequestId = Guid.NewGuid(), Kind = "idea", Text = new string('a', 1500), DocumentName = new string('b', 255) };
        Assert.True(Validator.TryValidateObject(valid, new ValidationContext(valid), [], true));
        var tooLong = new FeedbackRequest { Kind = "problem", Text = new string('a', 1501) };
        Assert.False(Validator.TryValidateObject(tooLong, new ValidationContext(tooLong), [], true));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(5, 360)]
    [InlineData(30, 360)]
    public void Delivery_retries_back_off_and_remain_bounded(int attempts, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), FeedbackEmail.RetryDelay(attempts));

    [Fact]
    public async Task Duplicate_signals_do_not_throw_or_leave_the_worker_blocked()
    {
        var signal = new FeedbackSignal();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(signal.Notify)));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await signal.WaitAsync(Timeout.InfiniteTimeSpan, timeout.Token);
        Assert.False(timeout.IsCancellationRequested);
    }
}
