using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Study;
using ExamFlow.Api.Features.Battles;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Health;
using ExamFlow.Api.Features.Focus;
using ExamFlow.Api.Features.Friends;
using ExamFlow.Api.Features.Groups;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Email;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtIdentity(builder.Configuration);

// Behind a reverse proxy (nginx, IIS, a cloud load balancer) the API must see the public scheme, host and client IP:
// OAuth redirect URIs, Secure cookies and per-IP rate limits depend on them. Loopback proxies are trusted by default;
// ForwardedHeaders:KnownProxies lists other proxy IPs.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
    // Managed hosts (Render) sit behind load balancers whose IPs are not published: trust the configured hop count.
    if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    // Only these public hosts are accepted from X-Forwarded-Host (empty = any; set it in production).
    options.AllowedHosts = builder.Configuration.GetSection("ForwardedHeaders:AllowedHosts").Get<string[]>() ?? [];
});
// Data Protection keys sign the OAuth state, external-login cookie and password-reset tokens. In containers the
// default key folder is lost on restart, so production should persist them (DataProtection:KeysPath on a volume).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("ExamR");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
else if (!builder.Environment.IsDevelopment())
    dataProtection.PersistKeysToDbContext<AppDbContext>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.AddSingleton<EmailQueue>();
// Brevo (HTTPS) in production — Render Free blocks outbound SMTP; SMTP / .eml pickup stays for local development.
var emailProvider = builder.Configuration["Email:Provider"]?.Trim().Trim('"');
if (string.Equals(emailProvider, "Brevo", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<IEmailTransport, BrevoTransport>(client => client.Timeout = TimeSpan.FromSeconds(20));
else
    builder.Services.AddSingleton<IEmailTransport, SmtpTransport>();
builder.Services.AddHostedService<SmtpEmailService>();
// Visible in the host's startup log, so a misconfigured provider is obvious without sending a test email.
Console.WriteLine($"Email provider: {(string.Equals(emailProvider, "Brevo", StringComparison.OrdinalIgnoreCase) ? "Brevo" : "SMTP")} (Email:Provider='{emailProvider}')");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Documents & PDF processing
builder.Services.Configure<DocumentOptions>(builder.Configuration.GetSection(DocumentOptions.Section));
// Local disk in development; Cloudflare R2 or an S3 endpoint (Supabase) where the disk is ephemeral.
if (builder.Configuration["Storage:Provider"] is { } provider
    && (provider.Equals("R2", StringComparison.OrdinalIgnoreCase) || provider.Equals("S3", StringComparison.OrdinalIgnoreCase)))
    builder.Services.AddSingleton<IFileStorage, R2FileStorage>();
else
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IAnswerDetector, MarkerAnswerDetector>(); // order = priority
builder.Services.AddSingleton<IAnswerDetector, StyleAnswerDetector>();
builder.Services.AddSingleton<PdfQuestionParser>();
builder.Services.AddSingleton<DocxQuestionParser>();
builder.Services.AddSingleton<ProcessingQueue>();
builder.Services.AddScoped<DocumentProcessor>();
builder.Services.AddHostedService<DocumentProcessingService>();

builder.Services.AddOptions<StudyAiOptions>()
    .Bind(builder.Configuration.GetSection(StudyAiOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddHttpClient<StudyAiClient>(client => client.Timeout = TimeSpan.FromSeconds(100));

builder.Services.Configure<ExamOptions>(builder.Configuration.GetSection(ExamOptions.Section));

// Battle mode: SignalR (built into ASP.NET Core) + in-memory rooms on this single server.
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 8 * 1024)
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<BattleService>();
builder.Services.AddHostedService<BattleCleanupService>();
builder.Services.AddSingleton<SocialPresence>();
builder.Services.AddSingleton<SocialEvents>();
builder.Services.Configure<SocialOptions>(builder.Configuration.GetSection("Social"));

// Request limits sit just above the PDF limit; the endpoint returns the friendly 413.
var maxUpload = builder.Configuration.GetValue<long?>($"{DocumentOptions.Section}:{nameof(DocumentOptions.MaxFileSizeBytes)}")
    ?? new DocumentOptions().MaxFileSizeBytes;
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = maxUpload + 1024 * 1024);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = maxUpload + 1024 * 1024);

var app = builder.Build();

app.UseForwardedHeaders();

// A person who lands on an /api URL in the browser (OAuth callback, an old link) must never see raw ProblemDetails JSON.
// Only top-level HTML navigations are affected; fetch/XHR clients keep receiving JSON errors as before.
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (!HttpMethods.IsGet(request.Method) || !request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
    {
        await next(context);
        return;
    }

    var original = context.Response.Body;
    await using var buffer = new MemoryStream();
    context.Response.Body = buffer;
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled error on browser navigation {Path}", request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    }
    finally
    {
        context.Response.Body = original;
    }

    if (context.Response.StatusCode >= 400)
    {
        var code = context.Response.StatusCode is 403 or 404 ? context.Response.StatusCode : context.Response.StatusCode >= 500 ? 500 : 404;
        var unauthorized = context.Response.StatusCode == StatusCodes.Status401Unauthorized;
        context.Response.Clear();
        context.Response.Redirect(unauthorized ? "/?auth=login" : $"/error?code={code}");
        return;
    }

    buffer.Position = 0;
    await buffer.CopyToAsync(original, context.RequestAborted);
});

// When the UI proxies /api and /hubs from its own domain (Cloudflare Pages → Render), the browser only ever sees that
// domain: OAuth redirect URIs and cookies must use it rather than the internal onrender.com address.
if (builder.Configuration["App:PublicOrigin"] is { Length: > 0 } publicOrigin)
{
    var origin = new Uri(publicOrigin);
    app.Use((context, next) =>
    {
        context.Request.Scheme = origin.Scheme;
        context.Request.Host = HostString.FromUriComponent(origin);
        return next(context);
    });
}

// Baseline security headers for every API response; HSTS only outside development (localhost runs over http).
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use((context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    return next(context);
});

// Malformed request bodies are client errors (400), not server errors.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapAuthEndpoints();
api.MapProfileEndpoints();
api.MapDocumentEndpoints();
api.MapDocumentSharingEndpoints();
api.MapQuestionEndpoints();
api.MapStudyEndpoints();
api.MapExamEndpoints();
api.MapBattleEndpoints();
api.MapFriendsEndpoints();
api.MapGroupEndpoints();
api.MapAdminEndpoints();
api.MapFocusEndpoints();
api.MapGoogleAuthEndpoints();
app.MapHub<BattleHub>(BattleHub.Path);
app.MapHub<SocialHub>("/hubs/social", options => options.CloseOnAuthenticationExpiration = true);

await app.SeedIdentityAsync();

app.Run();
