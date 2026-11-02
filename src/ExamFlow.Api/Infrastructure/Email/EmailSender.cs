using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Infrastructure.Email;

/// <summary>SMTP settings. Credentials come from user-secrets / environment variables, never from code.</summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "no-reply@examflow.local";
    public string FromName { get; set; } = "ExamR";

    /// <summary>Local testing without a mail server: messages are written as .eml files to this folder.</summary>
    public string? PickupDirectory { get; set; }
}

/// <summary>Provider choice. Brevo sends over HTTPS (works on hosts that block SMTP ports, e.g. Render Free).</summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>"Brevo" or "Smtp" (default).</summary>
    public string Provider { get; set; } = "Smtp";
    public string? BrevoApiKey { get; set; }
    /// <summary>Sender address; for Brevo it must be a verified sender. Falls back to Smtp:From.</summary>
    public string? From { get; set; }
    public string? FromName { get; set; }
    /// <summary>When true, password login is refused until the email address is confirmed.</summary>
    public bool RequireConfirmedEmail { get; set; }
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);

/// <summary>
/// Queues emails and sends them in the background, so request time does not depend on the provider
/// (and does not reveal whether a password-reset email was actually sent).
/// </summary>
public sealed class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>One way of delivering a message; swap providers by registering another implementation.</summary>
public interface IEmailTransport
{
    /// <returns>false when the transport is not configured and the message was skipped.</returns>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed class SmtpEmailService(EmailQueue queue, IEmailTransport transport, ILogger<SmtpEmailService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    if (await transport.SendAsync(message, stoppingToken))
                        logger.LogInformation("Email \"{Subject}\" sent", message.Subject);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Neither the recipient nor the body (it may contain a token) is logged.
                    logger.LogError(ex, "Sending email \"{Subject}\" failed", message.Subject);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

public sealed class SmtpTransport(IOptions<SmtpOptions> options, IHostEnvironment environment, ILogger<SmtpTransport> logger) : IEmailTransport
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        using var client = new SmtpClient();

        if (!string.IsNullOrWhiteSpace(settings.PickupDirectory))
        {
            var directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.PickupDirectory));
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Host))
        {
            client.Host = settings.Host;
            client.Port = settings.Port;
            client.EnableSsl = settings.EnableSsl;
            if (!string.IsNullOrEmpty(settings.UserName))
            {
                client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
            }
        }
        else
        {
            logger.LogWarning("No email provider is configured (Email:Provider=Brevo or Smtp:Host); email \"{Subject}\" was not sent", message.Subject);
            return false;
        }

        using var mail = new MailMessage { From = new MailAddress(settings.From, settings.FromName), Subject = message.Subject };
        // multipart/alternative: plain text first, HTML last (clients show the last part they support).
        if (message.TextBody is not null)
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, System.Text.Encoding.UTF8, "text/plain"));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, System.Text.Encoding.UTF8, "text/html"));
        mail.To.Add(message.To);

        await client.SendMailAsync(mail, ct);
        return true;
    }
}

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
