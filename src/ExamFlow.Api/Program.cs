using System.Text.Json.Serialization;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Study;
using ExamFlow.Api.Features.Battles;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Health;
using ExamFlow.Api.Features.Friends;
using ExamFlow.Api.Features.Groups;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure;
using ExamFlow.Api.Infrastructure.Email;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtIdentity(builder.Configuration);
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<SmtpEmailService>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Documents & PDF processing
builder.Services.Configure<DocumentOptions>(builder.Configuration.GetSection(DocumentOptions.Section));
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
app.MapHub<BattleHub>(BattleHub.Path);
app.MapHub<SocialHub>("/hubs/social", options => options.CloseOnAuthenticationExpiration = true);

await app.SeedIdentityAsync();

app.Run();
