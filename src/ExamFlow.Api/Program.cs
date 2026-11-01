using System.Text.Json.Serialization;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Health;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Parsing;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCookieIdentity();
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
builder.Services.AddSingleton<ProcessingQueue>();
builder.Services.AddScoped<DocumentProcessor>();
builder.Services.AddHostedService<DocumentProcessingService>();

builder.Services.Configure<ExamOptions>(builder.Configuration.GetSection(ExamOptions.Section));

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapAuthEndpoints();
api.MapDocumentEndpoints();
api.MapQuestionEndpoints();
api.MapExamEndpoints();

await app.SeedIdentityAsync();

app.Run();
