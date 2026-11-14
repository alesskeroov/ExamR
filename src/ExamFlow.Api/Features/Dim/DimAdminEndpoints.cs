using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Dim;

/// <summary>
/// Admin side of DİM tests. Questions come from the normal upload pipeline (PDF/DOCX → parser); here an admin fixes
/// the answer key, approves questions into the bank with a subject/topic, and builds tests from the bank.
/// </summary>
public static class DimAdminEndpoints
{
    public const int MaxOptions = 5;

    public static void MapDimAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/dim").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        group.MapGet("/documents", SourceDocuments);
        group.MapGet("/documents/{documentId:guid}/questions", SourceQuestions);
        group.MapPost("/documents/{documentId:guid}/approve-valid", ApproveValid).Validate<DimApproveDocumentRequest>();
        group.MapPost("/sets", CreateSet).Validate<DimCreateSetRequest>();
        group.MapPost("/questions", CreateQuestion).Validate<DimCreateQuestionRequest>();
        group.MapPut("/questions/{questionId:guid}", EditQuestion).Validate<DimEditQuestionRequest>();
        group.MapDelete("/questions/{questionId:guid}", DeleteQuestion);
        group.MapPost("/documents/{documentId:guid}/answer-key", AnswerKey).Validate<DimAnswerKeyRequest>();
        group.MapPost("/bank", Approve).Validate<DimApproveRequest>();
        group.MapDelete("/bank/{questionId:guid}", RemoveFromBank);
        group.MapGet("/bank/stats", BankStats);
        group.MapGet("/tests", Tests);
        group.MapPost("/tests", CreateTest).Validate<DimTestRequest>();
        group.MapPut("/tests/{id:guid}", UpdateTest).Validate<DimTestSettingsRequest>();
        group.MapDelete("/tests/{id:guid}", DeleteTest);
    }

    /// <summary>The admin's parsed documents with DİM-ready and banked question counts.</summary>
    private static async Task<IResult> SourceDocuments(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var documents = await db.Documents.AsNoTracking()
            .Where(d => d.UploadedByUserId == userId && d.Status == DocumentStatus.Completed)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id, d.OriginalFileName, d.CreatedAt, d.StoredFileName,
                Total = db.Questions.Count(q => q.DocumentId == d.Id),
                // Mirrors BankRejection: non-empty text and either a coded answer without options, or 2–5 non-empty
                // options with exactly one correct.
                Valid = db.Questions.Count(q => q.DocumentId == d.Id && q.Text.Trim() != ""
                    && ((q.CodedAnswer != null && q.Options.Count == 0)
                        || (q.Options.Count >= 2 && q.Options.Count <= MaxOptions
                            && q.Options.Count(o => o.IsCorrect) == 1 && !q.Options.Any(o => o.Text.Trim() == "")))),
            })
            .ToListAsync(ct);

        var ids = documents.Select(d => d.Id).ToList();
        var bank = await db.DimBankQuestions.AsNoTracking()
            .Where(b => ids.Contains(b.Question.DocumentId))
            .GroupBy(b => new { b.Question.DocumentId, b.Track, b.Subject })
            .Select(g => new { g.Key.DocumentId, g.Key.Track, g.Key.Subject, Count = g.Count() })
            .ToListAsync(ct);

        return TypedResults.Ok(documents.Select(d => new DimSourceDocumentDto(d.Id, d.OriginalFileName, d.CreatedAt, d.Total, d.Valid,
            bank.Where(b => b.DocumentId == d.Id).OrderBy(b => b.Track).ThenBy(b => b.Subject)
                .Select(b => new DimDocumentBankDto(b.Track, b.Subject, DimCatalog.SubjectName(b.Subject), b.Count)).ToList(),
            IsManualSet(d.StoredFileName))).ToList());
    }

    /// <summary>Questions of a document the admin uploaded, with their answer key and bank state.</summary>
    private static async Task<IResult> SourceQuestions(Guid documentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (!await db.Documents.AnyAsync(d => d.Id == documentId && d.UploadedByUserId == userId, ct))
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");

        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.DocumentId == documentId)
            .OrderBy(q => q.SourceNumber)
            .Select(q => new
            {
                q.Id, q.SourceNumber, q.Text, q.CodedAnswer, q.VerificationStatus, q.ParseWarning,
                Options = q.Options.OrderBy(o => o.Position).Select(o => new { o.Id, o.Text, o.IsCorrect }).ToList(),
                Bank = db.DimBankQuestions.Where(b => b.QuestionId == q.Id).Select(b => new DimBankInfoDto(b.Track, b.Subject, b.Topic)).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return TypedResults.Ok(questions.Select(q => new DimSourceQuestionDto(q.Id, q.SourceNumber, q.Text,
            q.Options.Select((o, i) => new DimOptionDto(o.Id, ((char)('A' + i)).ToString(), o.Text, o.IsCorrect)).ToList(),
            q.VerificationStatus, q.ParseWarning, q.Bank, q.CodedAnswer)).ToList());
    }

    /// <summary>Applies a typed answer key ("1-A 2-C …") to the document's questions by their original number.</summary>
    private static async Task<IResult> AnswerKey(
        Guid documentId, DimAnswerKeyRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (!await db.Documents.AnyAsync(d => d.Id == documentId && d.UploadedByUserId == userId, ct))
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");

        var (key, conflicts) = DimScoring.ParseAnswerKey(request.Text);
        if (key.Count == 0)
            return Problem(StatusCodes.Status400BadRequest, "Cavab açarında heç bir \"nömrə-variant\" cütü tapılmadı (məs. 1-A 2-C).");

        var numbers = key.Keys.ToList();
        var questions = await db.Questions.Include(q => q.Options)
            .Where(q => q.DocumentId == documentId && numbers.Contains(q.SourceNumber))
            .ToListAsync(ct);

        // Duplicated source numbers (rare parser output) all receive the same key.
        var targets = questions.Where(q => key[q.SourceNumber] < q.Options.Count).ToList();
        var invalid = questions.Except(targets).Select(q => q.SourceNumber).ToList();
        var applied = targets.Count;

        // UX_AnswerOptions_SingleCorrect allows one correct option per question at any moment, so the old answers are
        // cleared and saved before the new ones are set, in one transaction.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            foreach (var option in targets.SelectMany(q => q.Options)) option.IsCorrect = false;
            await db.SaveChangesAsync(ct);
            foreach (var question in targets)
            {
                question.Options.OrderBy(o => o.Position).ElementAt(key[question.SourceNumber]).IsCorrect = true;
                question.VerificationStatus = VerificationStatus.Verified;
                question.ParseWarning = null;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
        var found = questions.Select(q => q.SourceNumber).ToHashSet();
        return TypedResults.Ok(new DimAnswerKeyResultDto(applied, numbers.Where(n => !found.Contains(n)).Order().ToList(),
            invalid.Distinct().Order().ToList(), conflicts.Order().ToList()));
    }

    /// <summary>
    /// Why a question cannot enter the bank: it must be a closed A–E question with exactly one known answer, or an open
    /// coded question (no options) with its numeric answer.
    /// </summary>
    public static string? BankRejection(Question question) =>
        string.IsNullOrWhiteSpace(question.Text) ? "Sual mətni boşdur."
        : question.Options.Count == 0 ? (question.CodedAnswer is null ? "Variant yoxdur: kodlaşdırılan sual kimi cavabını yazın." : null)
        : question.Options.Count < 2 ? "Qapalı sualda ən azı 2 variant olmalıdır."
        : question.Options.Count > MaxOptions ? "Variantların sayı 5-dən çoxdur."
        : question.Options.Count(o => o.IsCorrect) != 1 ? "Düzgün cavab müəyyən edilməyib."
        : question.Options.Any(o => string.IsNullOrWhiteSpace(o.Text)) || string.IsNullOrWhiteSpace(question.Text) ? "Sual və ya variant mətni boşdur."
        : null;

    private static async Task<IResult> Approve(
        DimApproveRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!DimCatalog.IsSubjectOf(request.Track, request.Subject))
            return Problem(StatusCodes.Status400BadRequest, "Fənn bu imtahan növünə aid deyil.");

        var userId = user.GetUserId();
        var ids = request.QuestionIds.Distinct().ToList();
        var questions = await db.Questions.Include(q => q.Options)
            .Where(q => ids.Contains(q.Id) && q.Document.UploadedByUserId == userId)
            .ToListAsync(ct);
        var rejected = ids.Where(id => questions.All(q => q.Id != id)).Select(id => new DimRejectedDto(id, "Sual tapılmadı.")).ToList();
        rejected.AddRange(await AddToBankAsync(db, questions, request.Track, request.Subject, request.Topic, userId, clock.GetUtcNow(), ct));
        await MarkDimSourcesAsync(db, questions.Select(q => q.DocumentId).Distinct().ToList(), ct);
        return TypedResults.Ok(new DimApproveResultDto(ids.Count - rejected.Count, rejected));
    }

    /// <summary>One click: every question of the document that fits DİM tests goes into the bank; the rest are reported.</summary>
    private static async Task<IResult> ApproveValid(
        Guid documentId, DimApproveDocumentRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!DimCatalog.IsSubjectOf(request.Track, request.Subject))
            return Problem(StatusCodes.Status400BadRequest, "Fənn bu imtahan növünə aid deyil.");

        var userId = user.GetUserId();
        if (!await db.Documents.AnyAsync(d => d.Id == documentId && d.UploadedByUserId == userId, ct))
            return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");

        var questions = await db.Questions.Include(q => q.Options).Where(q => q.DocumentId == documentId).ToListAsync(ct);
        var rejected = await AddToBankAsync(db, questions, request.Track, request.Subject, request.Topic, userId, clock.GetUtcNow(), ct);
        await MarkDimSourcesAsync(db, [documentId], ct);
        return TypedResults.Ok(new DimApproveResultDto(questions.Count - rejected.Count, rejected));
    }

    /// <summary>A document whose questions feed the DİM bank is hidden from exams, battles and document lists.</summary>
    private static Task<int> MarkDimSourcesAsync(AppDbContext db, List<Guid> documentIds, CancellationToken ct) =>
        db.Documents.Where(d => documentIds.Contains(d.Id) && !d.IsDimSource)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsDimSource, true), ct);

    /// <summary>
    /// Fixes a question in place: for a closed question its text, option texts and the single correct option; for a
    /// question without options its text and coded answer (which turns a parsed open question into a coded one).
    /// </summary>
    private static async Task<IResult> EditQuestion(
        Guid questionId, DimEditQuestionRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var question = await db.Questions.Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.Document.UploadedByUserId == userId, ct);
        if (question is null) return Problem(StatusCodes.Status404NotFound, "Sual tapılmadı.");
        if (question.Options.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(request.Text)) return Problem(StatusCodes.Status400BadRequest, "Sual mətni boş ola bilməz.");
            var (key, keyError) = CodedAnswers.NormalizeKey(request.CodedAnswer);
            if (keyError is not null) return Problem(StatusCodes.Status400BadRequest, keyError);
            question.Text = request.Text.Trim();
            question.CodedAnswer = key;
            question.VerificationStatus = VerificationStatus.Verified;
            question.ParseWarning = null;
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }

        if (question.Options.Count < 2 || question.Options.Count > MaxOptions)
            return Problem(StatusCodes.Status400BadRequest, "Bu sualın variant sayı DİM formatına uyğun deyil (2–5 variant olmalıdır).");
        if (request.Options.Count != question.Options.Count || question.Options.Any(o => request.Options.All(r => r.Id != o.Id)))
            return Problem(StatusCodes.Status400BadRequest, "Variantlar sualın variantlarına uyğun gəlmir.");
        if (question.Options.All(o => o.Id != request.CorrectOptionId))
            return Problem(StatusCodes.Status400BadRequest, "Düzgün cavabı seçin.");
        if (string.IsNullOrWhiteSpace(request.Text) || request.Options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
            return Problem(StatusCodes.Status400BadRequest, "Sual və variant mətnləri boş ola bilməz.");

        // UX_AnswerOptions_SingleCorrect allows one correct option at any moment: clear, save, then set the new one.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            foreach (var option in question.Options) option.IsCorrect = false;
            await db.SaveChangesAsync(ct);
            question.Text = request.Text.Trim();
            foreach (var option in question.Options)
            {
                option.Text = request.Options.First(o => o.Id == option.Id).Text.Trim();
                option.IsCorrect = option.Id == request.CorrectOptionId;
            }

            question.VerificationStatus = VerificationStatus.Verified;
            question.ParseWarning = null;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
        return TypedResults.NoContent();
    }

    /// <summary>File-name suffix of a hand-made question set: a document row without a stored file.</summary>
    public const string ManualSetSuffix = ".manual";

    internal static bool IsManualSet(string storedFileName) => storedFileName.EndsWith(ManualSetSuffix, StringComparison.Ordinal);

    /// <summary>
    /// An empty set for questions typed in by hand. It is an ordinary document (so tests can draw from it like from an
    /// uploaded PDF) that has no file and is hidden from users from the start.
    /// </summary>
    private static async Task<IResult> CreateSet(
        DimCreateSetRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var id = Guid.NewGuid();
        var document = new UploadedDocument
        {
            Id = id,
            OriginalFileName = request.Title.Trim(),
            StoredFileName = $"{id:N}{ManualSetSuffix}",
            // Unique per user like an upload's file hash; there is no file, so the id stands in for its content.
            Sha256 = System.Security.Cryptography.SHA256.HashData(id.ToByteArray()),
            IsDimSource = true,
            Status = DocumentStatus.Completed,
            UploadedByUserId = user.GetUserId(),
            CreatedAt = now,
            UpdatedAt = now,
            ProcessedAt = now,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/admin/dim/documents/{id}",
            new DimSourceDocumentDto(id, document.OriginalFileName, now, 0, 0, [], IsManual: true));
    }

    /// <summary>
    /// Adds one question typed in by hand to a document of the admin (usually a manual set) and puts it straight into
    /// the bank. Without options it is an open coded question and <see cref="DimCreateQuestionRequest.CodedAnswer"/>
    /// is required; otherwise 2–5 options with one correct.
    /// </summary>
    private static async Task<IResult> CreateQuestion(
        DimCreateQuestionRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!DimCatalog.IsSubjectOf(request.Track, request.Subject))
            return Problem(StatusCodes.Status400BadRequest, "Fənn bu imtahan növünə aid deyil.");

        var userId = user.GetUserId();
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.UploadedByUserId == userId, ct);
        if (document is null) return Problem(StatusCodes.Status404NotFound, "Sənəd tapılmadı.");
        if (document.Status != DocumentStatus.Completed)
            return Problem(StatusCodes.Status409Conflict, "Sənəd hələ emal olunur, sonra yenidən cəhd edin.");

        var text = request.Text.Trim();
        var options = request.Options.Select(o => o.Trim()).ToList();
        if (text.Length == 0) return Problem(StatusCodes.Status400BadRequest, "Sual mətnini yazın.");

        string? codedAnswer = null;
        if (options.Count == 0)
        {
            var (key, keyError) = CodedAnswers.NormalizeKey(request.CodedAnswer);
            if (keyError is not null) return Problem(StatusCodes.Status400BadRequest, keyError);
            codedAnswer = key;
        }
        else
        {
            if (options.Count < 2 || options.Count > MaxOptions)
                return Problem(StatusCodes.Status400BadRequest, $"Qapalı sualda 2–{MaxOptions} variant olmalıdır.");
            if (options.Any(o => o.Length == 0)) return Problem(StatusCodes.Status400BadRequest, "Variant mətni boş ola bilməz.");
            if (request.CorrectIndex is not { } correct || correct < 0 || correct >= options.Count)
                return Problem(StatusCodes.Status400BadRequest, "Düzgün variantı seçin.");
        }

        var now = clock.GetUtcNow();
        var lastNumber = await db.Questions.Where(q => q.DocumentId == document.Id).MaxAsync(q => (int?)q.SourceNumber, ct) ?? 0;
        var question = new Question
        {
            DocumentId = document.Id,
            SourceNumber = lastNumber + 1,
            Text = text,
            CodedAnswer = codedAnswer,
            // The coded answer is part of the identity: the same stem with another answer is another question.
            ContentHash = ExamFlow.Parsing.ContentHasher.Compute(text, codedAnswer is null ? options : [$"\0coded:{codedAnswer}"]),
            VerificationStatus = VerificationStatus.Verified,
            CreatedAt = now,
            UpdatedAt = now,
            Options = options.Select((o, i) => new AnswerOption
            {
                Position = (byte)i,
                Label = ((char)('A' + i)).ToString(),
                Text = o,
                IsCorrect = i == request.CorrectIndex,
            }).ToList(),
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);

        var rejected = await AddToBankAsync(db, [question], request.Track, request.Subject, request.Topic, userId, now, ct);
        if (rejected.Count > 0) return Problem(StatusCodes.Status400BadRequest, rejected[0].Reason);
        await MarkDimSourcesAsync(db, [document.Id], ct);

        var topic = string.IsNullOrWhiteSpace(request.Topic) ? null : request.Topic.Trim();
        return TypedResults.Created($"/api/admin/dim/questions/{question.Id}", new DimSourceQuestionDto(question.Id, question.SourceNumber, question.Text,
            question.Options.Select(o => new DimOptionDto(o.Id, o.Label, o.Text, o.IsCorrect)).ToList(), question.VerificationStatus, null,
            new DimBankInfoDto(request.Track, request.Subject, topic), question.CodedAnswer));
    }

    /// <summary>
    /// Deletes a question of a manual set (with its bank row). Questions of an uploaded PDF are only removed from the
    /// bank, since the PDF would bring them back and their images belong to it. Past exams keep their snapshot.
    /// </summary>
    private static async Task<IResult> DeleteQuestion(Guid questionId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var question = await db.Questions
            .Where(q => q.Id == questionId && q.Document.UploadedByUserId == userId)
            .Select(q => new { q.Id, q.Document.StoredFileName })
            .FirstOrDefaultAsync(ct);
        if (question is null) return Problem(StatusCodes.Status404NotFound, "Sual tapılmadı.");
        if (!IsManualSet(question.StoredFileName))
            return Problem(StatusCodes.Status400BadRequest, "Yalnız əl ilə əlavə olunmuş suallar silinə bilər; PDF sualını bankdan çıxarın.");

        await db.Questions.Where(q => q.Id == questionId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Adds (or re-classifies) the questions that fit DİM tests; returns the ones that do not, with the reason.</summary>
    private static async Task<List<DimRejectedDto>> AddToBankAsync(
        AppDbContext db, List<Question> questions, DimTrack track, string subject, string? topicText, Guid userId, DateTimeOffset now,
        CancellationToken ct)
    {
        var ids = questions.Select(q => q.Id).ToList();
        var existing = await db.DimBankQuestions.Where(b => ids.Contains(b.QuestionId)).ToDictionaryAsync(b => b.QuestionId, ct);
        var topic = string.IsNullOrWhiteSpace(topicText) ? null : topicText.Trim();
        var rejected = new List<DimRejectedDto>();
        foreach (var question in questions)
        {
            if (BankRejection(question) is { } reason)
            {
                rejected.Add(new DimRejectedDto(question.Id, reason));
                continue;
            }

            question.VerificationStatus = VerificationStatus.Verified;
            if (!existing.TryGetValue(question.Id, out var bank))
            {
                bank = new DimBankQuestion { QuestionId = question.Id };
                db.DimBankQuestions.Add(bank);
            }

            bank.Track = track;
            bank.Subject = subject;
            bank.Topic = topic;
            bank.ApprovedByUserId = userId;
            bank.ApprovedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return rejected;
    }

    private static async Task<IResult> RemoveFromBank(Guid questionId, AppDbContext db, CancellationToken ct) =>
        await db.DimBankQuestions.Where(b => b.QuestionId == questionId).ExecuteDeleteAsync(ct) == 0
            ? Problem(StatusCodes.Status404NotFound, "Sual bankda tapılmadı.")
            : TypedResults.NoContent();

    private static async Task<IResult> BankStats(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.DimBankQuestions.AsNoTracking()
            .GroupBy(b => new { b.Track, b.Subject })
            .Select(g => new { g.Key.Track, g.Key.Subject, Count = g.Count(), Topics = g.Select(b => b.Topic).Distinct().Count(t => t != null) })
            .ToListAsync(ct);
        return TypedResults.Ok(rows.OrderBy(r => r.Track).ThenBy(r => r.Subject)
            .Select(r => new DimBankStatDto(r.Track, r.Subject, DimCatalog.SubjectName(r.Subject), r.Count, r.Topics)).ToList());
    }

    private static async Task<IResult> Tests(AppDbContext db, CancellationToken ct)
    {
        var tests = await db.DimTests.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        var attempts = await db.DimAttempts.AsNoTracking().GroupBy(a => a.TestId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return TypedResults.Ok(tests.Select(t => ToAdminDto(t, attempts.GetValueOrDefault(t.Id))).ToList());
    }

    private static async Task<IResult> CreateTest(
        DimTestRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ValidateShape(request) is { } error) return Problem(StatusCodes.Status400BadRequest, error);
        if (ValidateWindow(request.AvailableFrom, request.AvailableTo) is { } windowError) return Problem(StatusCodes.Status400BadRequest, windowError);

        var sections = new List<DimTestSection>();
        foreach (var section in request.Sections)
        {
            var bank = await BankCandidatesAsync(db, request.Track, section.Subject, section.SourceDocumentId, ct);
            var name = DimCatalog.SubjectName(section.Subject);
            if (section.QuestionIds.Count > 0)
            {
                var ids = section.QuestionIds.Distinct().ToList();
                var inBank = bank.Select(c => c.Id).ToHashSet();
                if (ids.Any(id => !inBank.Contains(id)))
                    return Problem(StatusCodes.Status400BadRequest, $"{name}: seçilmiş sualların bəziləri bu fənnin sual bankında deyil.");
                sections.Add(new DimTestSection { Subject = section.Subject, QuestionIds = ids });
                continue;
            }

            if (section.SourceDocumentId is not null && bank.Count == 0)
                return Problem(StatusCodes.Status400BadRequest, $"{name}: seçilmiş sənəddən bu fənn üzrə bankda sual yoxdur.");
            if (section.RandomCount < 1)
                return Problem(StatusCodes.Status400BadRequest, $"{name}: sual seçin və ya təsadüfi sual sayını göstərin.");
            if (ExamEngine.CountDistinct(bank) < section.RandomCount)
                return Problem(StatusCodes.Status422UnprocessableEntity,
                    $"{name}: bankda cəmi {ExamEngine.CountDistinct(bank)} unikal sual var, {section.RandomCount} tələb olunur.");

            sections.Add(section.DrawPerAttempt
                ? new DimTestSection { Subject = section.Subject, RandomCount = section.RandomCount, SourceDocumentId = section.SourceDocumentId }
                : new DimTestSection { Subject = section.Subject, QuestionIds = ExamEngine.PickQuestions(bank, section.RandomCount, Random.Shared) });
        }

        if (sections.Sum(s => s.QuestionCount) > ExamOptions.HardMaxQuestions)
            return Problem(StatusCodes.Status400BadRequest, $"Sınaqda ən çox {ExamOptions.HardMaxQuestions} sual ola bilər.");

        var test = new DimTest
        {
            Track = request.Track,
            Kind = request.Kind,
            GroupCode = request.Track == DimTrack.Blok && request.Kind == DimTestKind.Full ? request.GroupCode : null,
            Title = request.Title.Trim(),
            DurationMinutes = (short)request.DurationMinutes,
            AttemptLimit = (short?)request.AttemptLimit,
            AvailableFrom = request.AvailableFrom,
            AvailableTo = request.AvailableTo,
            IsPublished = request.IsPublished,
            CreatedByUserId = user.GetUserId(),
            CreatedAt = clock.GetUtcNow(),
            Sections = sections,
        };
        db.DimTests.Add(test);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/admin/dim/tests/{test.Id}", ToAdminDto(test, 0));
    }

    private static async Task<IResult> UpdateTest(Guid id, DimTestSettingsRequest request, AppDbContext db, CancellationToken ct)
    {
        if (ValidateWindow(request.AvailableFrom, request.AvailableTo) is { } error) return Problem(StatusCodes.Status400BadRequest, error);
        var test = await db.DimTests.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (test is null) return NotFound();

        test.Title = request.Title.Trim();
        test.DurationMinutes = (short)request.DurationMinutes;
        test.AttemptLimit = (short?)request.AttemptLimit;
        test.AvailableFrom = request.AvailableFrom;
        test.AvailableTo = request.AvailableTo;
        test.IsPublished = request.IsPublished;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToAdminDto(test, await db.DimAttempts.CountAsync(a => a.TestId == id, ct)));
    }

    private static async Task<IResult> DeleteTest(Guid id, AppDbContext db, CancellationToken ct)
    {
        // Attempts are ordinary exams; deleting them cascades to their DimAttempt rows, then the test can go.
        // The retrying execution strategy requires a user transaction to run inside it.
        var deleted = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var attemptExamIds = db.DimAttempts.Where(a => a.TestId == id).Select(a => a.ExamId);
            await db.Exams.Where(e => attemptExamIds.Contains(e.Id)).ExecuteDeleteAsync(ct);
            var count = await db.DimTests.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
            return count;
        });
        return deleted == 0 ? NotFound() : TypedResults.NoContent();
    }

    /// <summary>Approved bank questions of one subject, as exam candidates (all are gradable).</summary>
    internal static Task<List<ExamCandidate>> BankCandidatesAsync(
        AppDbContext db, DimTrack track, string subject, Guid? documentId, CancellationToken ct) =>
        db.DimBankQuestions.AsNoTracking()
            .Where(b => b.Track == track && b.Subject == subject && (documentId == null || b.Question.DocumentId == documentId))
            .Select(b => new ExamCandidate(b.QuestionId, b.Question.ContentHash, true))
            .ToListAsync(ct);

    public static string? ValidateShape(DimTestRequest request)
    {
        var subjects = request.Sections.Select(s => s.Subject).ToList();
        if (subjects.Distinct().Count() != subjects.Count) return "Hər fənn sınaqda bir dəfə ola bilər.";
        if (subjects.Any(s => !DimCatalog.IsSubjectOf(request.Track, s))) return "Fənn bu imtahan növünə aid deyil.";

        if (request.Kind == DimTestKind.Subject)
            return subjects.Count == 1 ? null : "Fənn sınağında yalnız bir fənn olmalıdır.";

        if (request.Track == DimTrack.Buraxilis)
            return subjects.Order().SequenceEqual(DimCatalog.Buraxilis.Select(s => s.Code).Order()) ? null
                : "Buraxılış tam sınağı Azərbaycan dili, Riyaziyyat və Xarici dil fənlərini əhatə etməlidir.";

        return DimCatalog.Group(request.GroupCode) is not { } group ? "Blok tam sınağı üçün ixtisas qrupu seçin."
            : subjects.Order().SequenceEqual(group.Subjects.Select(s => s.Code).Order()) ? null
            : $"{group.Name} sınağı qrupun üç fənnini əhatə etməlidir.";
    }

    private static string? ValidateWindow(DateTimeOffset? from, DateTimeOffset? to) =>
        from is not null && to is not null && from >= to ? "Başlama vaxtı bitmə vaxtından əvvəl olmalıdır." : null;

    internal static DimSectionDto ToSectionDto(DimTestSection s) =>
        new(s.Subject, DimCatalog.SubjectName(s.Subject), s.QuestionCount, s.QuestionIds.Count == 0, s.SourceDocumentId);

    private static DimAdminTestDto ToAdminDto(DimTest t, int attempts) => new(
        t.Id, t.Track, t.Kind, t.GroupCode, t.Title, t.DurationMinutes, t.AttemptLimit, t.AvailableFrom, t.AvailableTo,
        t.IsPublished, t.CreatedAt, t.Sections.Select(ToSectionDto).ToList(), attempts);

    private static IResult NotFound() => Problem(StatusCodes.Status404NotFound, "Sınaq tapılmadı.");

    private static IResult Problem(int status, string detail) => TypedResults.Problem(detail, statusCode: status);
}
