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

/// <summary>
/// SMTP delivery (Gmail: smtp.gmail.com:587 with STARTTLS and a Google App Password). Values come from environment
/// variables (Email__SmtpHost, Email__Username, …); the older Smtp:* section is still read as a fallback.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>"Gmail" (Gmail API over HTTPS), "Brevo" (HTTPS API) or "Smtp" (default).</summary>
    public string Provider { get; set; } = "Smtp";
    /// <summary>Sender address for the HTTPS providers (Gmail: the authorised account or one of its aliases).</summary>
    public string? From { get; set; }
    /// <summary>Display name for the HTTPS providers.</summary>
    public string? FromName { get; set; }
    public string? BrevoApiKey { get; set; }
    public GmailApiOptions Gmail { get; set; } = new();

    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? SenderName { get; set; }
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

public sealed class SmtpTransport(IOptions<EmailOptions> email, IOptions<SmtpOptions> options, IHostEnvironment environment, ILogger<SmtpTransport> logger) : IEmailTransport
{
    private const int Attempts = 3;

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        var e = email.Value;
        var host = e.SmtpHost ?? settings.Host;
        var user = e.Username ?? settings.UserName;

        using var mail = new MailMessage
        {
            // Gmail only relays mail from the authenticated account, so the sender is the SMTP user.
            From = new MailAddress(user ?? settings.From, e.SenderName ?? settings.FromName),
            Subject = message.Subject,
        };
        // multipart/alternative: plain text first, HTML last (clients show the last part they support).
        if (message.TextBody is not null)
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, System.Text.Encoding.UTF8, "text/plain"));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, System.Text.Encoding.UTF8, "text/html"));
        mail.To.Add(message.To);

        if (!string.IsNullOrWhiteSpace(settings.PickupDirectory))
        {
            using var pickup = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = Directory.CreateDirectory(Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.PickupDirectory))).FullName,
            };
            await pickup.SendMailAsync(mail, ct);
            return true;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("SMTP is not configured (Email:SmtpHost); email \"{Subject}\" was not sent", message.Subject);
            return false;
        }

        for (var attempt = 1; ; attempt++)
        {
            // TLS is always required (port 587 = STARTTLS); credentials never reach the logs.
            using var client = new SmtpClient(host, e.SmtpPort ?? settings.Port) { EnableSsl = true, Timeout = 20_000 };
            if (!string.IsNullOrEmpty(user))
                client.Credentials = new NetworkCredential(user, e.Password ?? settings.Password);
            try
            {
                await client.SendMailAsync(mail, ct);
                return true;
            }
            catch (SmtpException ex) when (attempt < Attempts && IsTransient(ex))
            {
                logger.LogWarning("SMTP attempt {Attempt} for \"{Subject}\" failed ({Status}); retrying", attempt, message.Subject, ex.StatusCode);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }
    }

    // Busy/unavailable servers and dropped connections are worth a retry; authentication or address errors are not.
    private static bool IsTransient(SmtpException ex) => ex.StatusCode is SmtpStatusCode.ServiceNotAvailable or SmtpStatusCode.MailboxBusy
        or SmtpStatusCode.TransactionFailed or SmtpStatusCode.LocalErrorInProcessing or SmtpStatusCode.InsufficientStorage
        || ex.InnerException is IOException or System.Net.Sockets.SocketException;
}
