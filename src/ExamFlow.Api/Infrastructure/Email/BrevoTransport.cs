using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Infrastructure.Email;

/// <summary>Brevo transactional email over HTTPS (https://developers.brevo.com/reference/sendtransacemail).</summary>
public sealed class BrevoTransport(HttpClient http, IOptions<EmailOptions> options, IOptions<SmtpOptions> smtp, ILogger<BrevoTransport> logger) : IEmailTransport
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BrevoApiKey))
        {
            logger.LogWarning("Email:BrevoApiKey is not configured; email \"{Subject}\" was not sent", message.Subject);
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { email = settings.From ?? smtp.Value.From, name = settings.FromName ?? smtp.Value.FromName },
                to = new[] { new { email = message.To } },
                subject = message.Subject,
                htmlContent = message.HtmlBody,
                textContent = message.TextBody,
            }),
        };
        request.Headers.Add("api-key", settings.BrevoApiKey);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Brevo's error body describes the problem (unverified sender, quota) and contains no secrets.
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Brevo responded {(int)response.StatusCode}: {detail[..Math.Min(detail.Length, 300)]}");
        }
        return true;
    }
}
