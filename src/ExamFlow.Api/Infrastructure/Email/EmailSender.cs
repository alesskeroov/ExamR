using System.Net;
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

public sealed record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>
/// Queues emails and sends them in the background, so request time does not depend on SMTP
/// (and does not reveal whether a password-reset email was actually sent).
/// </summary>
public sealed class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class SmtpEmailService(
    EmailQueue queue,
    IOptions<SmtpOptions> options,
    IHostEnvironment environment,
    ILogger<SmtpEmailService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await SendAsync(message, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The recipient address is not logged on purpose.
                    logger.LogError(ex, "Sending email \"{Subject}\" failed", message.Subject);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken ct)
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
            logger.LogWarning("SMTP is not configured (Smtp:Host or Smtp:PickupDirectory); email \"{Subject}\" was not sent", message.Subject);
            return;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.From, settings.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.To);

        await client.SendMailAsync(mail, ct);
        logger.LogInformation("Email \"{Subject}\" sent", message.Subject);
    }
}
