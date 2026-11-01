# Architecture

## 1. Arxitektura seçimi

### Qiymətləndirmə (1 = zəif, 5 = güclü)

| Meyar | Layered | Clean Architecture | Vertical Slice | **Modular Monolith (feature-based)** | Microservices |
|---|---|---|---|---|---|
| Maintainability | 3 | 4 | 4 | **5** | 3 |
| Scalability | 3 | 3 | 3 | **4** | 5 |
| Testability | 3 | 5 | 4 | **4** | 4 |
| Performance | 4 | 4 | 5 | **5** | 3 |
| Development complexity (aşağı = yaxşı) | 4 | 2 | 4 | **4** | 1 |
| Simplicity | 4 | 2 | 4 | **4** | 1 |
| Future extensibility | 2 | 4 | 4 | **5** | 5 |

### Qərar: Feature-based Modular Monolith (daxildə vertical slice)

- Layihədə **3 aydın domen modulu** var: Documents (PDF + parsing), Questions (bank), Exams (sessiya + nəticə). Hər modul öz endpoint-lərini, servislərini, DTO-larını öz qovluğunda saxlayır.
- **Ən mürəkkəb və ən çox test tələb edən hissə — PDF parser — ayrıca class library-dir** (`ExamFlow.Parsing`). EF Core-dan, HTTP-dən asılı deyil; təmiz input (stream) → output (`ParsedQuestion[]`). Bu, Clean Architecture-ın əsas faydasını (test edilə bilən core) yalnız lazım olan yerdə verir.
- Clean Architecture-ın tam 4 layihəli forması (Domain/Application/Infrastructure/Api + repository + MediatR) bu miqyas üçün ceremony-dir: hər feature üçün 5–6 fayl, xeyri az.
- Microservices **lazım deyil**: bir komanda, bir DB, sinxron əlaqəli domenlər. Yeganə ağır iş (PDF emalı) in-process background queue ilə həll olunur. Gələcəkdə OCR ağır olarsa, `ExamFlow.Parsing` ayrıca worker-ə çıxarıla bilər — sərhəd artıq hazırdır.

**Çatışmazlıqlar və qarşı tədbir:**
- Modullar eyni `DbContext`-i paylaşır → modullar arası sıx bağlılıq riski. Qayda: bir modul başqa modulun cədvəlinə **yalnız oxumaq** üçün müraciət edə bilər; yazma həmişə sahib modulun servisi vasitəsilə.
- Repository qatı yoxdur → endpoint handler-ləri `DbContext`-i birbaşa istifadə edir. Saf məntiq (`ExamEngine`, parser) ayrıca unit test olunur; real SQL Server ilə inteqrasiya testləri (Testcontainers) planlaşdırılır (`TASKS.md`).

## 2. Texnologiya qərarları

| Sahə | Seçim | Səbəb |
|---|---|---|
| Runtime | **.NET 9** | Kompüterdə mövcud SDK (istifadəçi qərarı). .NET 9 dəstəyi noyabr 2026-da bitir → **.NET 10 LTS-ə keçid** ayrıca kiçik tapşırıqdır (TFM + paket versiyaları) |
| API stili | **Minimal APIs** + `MapGroup` per feature | Feature-qovluq strukturuna təbii uyğun, controller-dən az boilerplate |
| ORM | EF Core 9 + `Microsoft.EntityFrameworkCore.SqlServer` | Tələb |
| Database | **SQL Server 2022** | Tələb; dev-də lokal instance, prod-da SQL Server / Azure SQL |
| Auth | **ASP.NET Core Identity + JWT** (`Microsoft.AspNetCore.Authentication.JwtBearer`) | İstifadəçi tələbi. HS256, 60 dəq access token, issuer/audience/lifetime yoxlanılır; SecurityStamp claim-i ilə logout/şifrə bərpası köhnə tokenləri ləğv edir. Refresh token yoxdur (müddət bitəndə yenidən giriş) |
| Email | `System.Net.Mail` SMTP, arxa plan növbəsi (`EmailQueue`) | Əlavə paket yoxdur; dev-də `Smtp:PickupDirectory` ilə `.eml` faylı |
| Validation | DataAnnotations + kiçik `ValidationFilter<T>` endpoint filter (`Validator.TryValidateObject`) + servisdə domen qaydaları | .NET 9-da Minimal API built-in validation yoxdur; FluentValidation əlavə dependency-dir, MVP üçün lazım deyil |
| Mapping | Əl ilə (`Select` projection) | AutoMapper lazımsızdır; projection SQL-i də optimallaşdırır |
| Background iş | `Channel<Guid>` + `BackgroundService` | Redis/RabbitMQ lazım deyil; restart-da `Pending` sənədlər yenidən növbəyə qoyulur |
| Fayl saxlama | `IFileStorage` → lokal disk | Sonradan Azure Blob/S3-ə keçid üçün abstraksiya |
| Logging | Built-in `ILogger` (console + JSON) | Serilog lazım olduqda sonra |
| Test | xUnit: parser testləri (generasiya olunmuş + real PDF-lər), `ExamEngine` və `TokenService` unit testləri. Testcontainers.MsSql + `WebApplicationFactory` inteqrasiya testləri planlaşdırılır | Saf məntiq DB-siz sürətli test olunur |
| Konfiqurasiya | Connection string `appsettings.json`-da **yoxdur**; dev: `dotnet user-secrets`, prod: environment variable `ConnectionStrings__Default` | Gizli məlumat repo-ya düşmür |

### PDF kitabxanası müqayisəsi

| Kitabxana | Lisenziya | Mətn çıxarışı | Şrift/bold/rəng məlumatı | Qərar |
|---|---|---|---|---|
| **PdfPig** (NuGet: `PdfPig`) | Apache 2.0 | Yaxşı, söz/hərf səviyyəsində koordinatlar | **Var** (`Letter.FontName`, `Font.IsBold`, `Color`) | **Seçildi** |
| iText 7/8 | AGPL / kommersiya | Çox yaxşı | Var | Lisenziya riski |
| PDFsharp | MIT | Zəif (əsasən yaratma üçün) | Yox | Uyğun deyil |
| Docnet / PDFium | MIT/BSD | Yaxşı | Məhdud | Native dependency, stil məlumatı zəif |
| Aspose.PDF | Kommersiya | Çox yaxşı | Var | Bahalı |

Bold aşkarlanması tələbi PdfPig-i yeganə uyğun açıq lisenziyalı seçim edir.

## 3. Solution strukturu

```
ExamFlow/
├─ AGENTS.md, CLAUDE.md, TASKS.md   (agent qaydaları və ötürmə — bax §6)
├─ src/
│  ├─ ExamFlow.Api/
│  │  ├─ Features/
│  │  │  ├─ Auth/            AuthEndpoints (register/login/logout/me/forgot/reset), AppUser, IdentitySeeder
│  │  │  ├─ Documents/       DocumentEndpoints (upload/list/get/reprocess/delete), DocumentProcessing
│  │  │  │                   (ProcessingQueue, DocumentProcessingService, DocumentProcessor), UploadedDocument
│  │  │  ├─ Questions/       QuestionEndpoints (read-only siyahı), Question, AnswerOption
│  │  │  ├─ Exams/           ExamEndpoints, ExamEngine (seçim/qarışdırma/qiymətləndirmə), Exam, ExamDtos, ExamOptions
│  │  │  ├─ Battles/         BattleHub (SignalR), BattleService (yaddaşda otaqlar + oyun dövrü), BattleScoring,
│  │  │  │                   BattleEndpoints (otaq yarat, tarixçə), BattleResult, BattleCleanupService
│  │  │  └─ Health/          /api/health (DB bağlantısı)
│  │  ├─ Infrastructure/
│  │  │  ├─ Data/            AppDbContext, AuditInterceptor, Configurations/, Migrations/
│  │  │  ├─ Security/        AuthenticationSetup (Identity + JWT + rate limit), TokenService, ClaimsPrincipalExtensions
│  │  │  ├─ Email/           EmailQueue + SmtpEmailService (arxa plan)
│  │  │  ├─ Storage/         IFileStorage, LocalFileStorage
│  │  │  └─ DependencyInjection.cs (DbContext + connection string yoxlaması)
│  │  ├─ Common/             Paging, ValidationFilter<T>, IAuditable
│  │  └─ Program.cs          servislərin qeydiyyatı, middleware, endpoint qrupları
│  └─ ExamFlow.Parsing/      PdfQuestionParser → PdfTextExtractor → LayoutCleaner → QuestionSegmenter
│                            → IAnswerDetector (Marker, Style) → ContentHasher. EF/HTTP asılılığı yoxdur
├─ tests/
│  ├─ ExamFlow.Parsing.Tests/  TestPdf (PDF generatoru), parser/segmenter testləri, RealSampleTests
│  └─ ExamFlow.Api.Tests/      ExamEngineTests, TokenServiceTests (unit)
├─ client/src/
│  ├─ app/                   router, providers (QueryClient, 401 → çıxış), AppLayout, RequireAuth
│  ├─ features/              auth, dashboard, documents, questions, exams, battle — hər birində api.ts + səhifələr
│  │                         (battle/api.ts: `useBattleRoom` — SignalR bağlantısı, auto-reconnect + yenidən JoinRoom)
│  ├─ components/common/     Button, FormField, Alert, EmptyState, FileDropzone, StatusBadge, PageHeader
│  └─ lib/                   api-client (fetch + JWT), auth-token, query-keys, format, form-errors
├─ samples/                  real nümunə PDF-lər (Git-də deyil)
└─ docs/
```

Hər backend modulu: `XxxEndpoints.cs` (route-lar + handler-lər), entity-lər, `XxxDtos.cs`. Ayrıca servis sinfi yalnız saf məntiq üçün yaradılır (`ExamEngine`); DB işi handler-lərdədir.

## 4. Deploy modeli

- **Dev:** Vite dev server (`:5190`) `/api` sorğularını ASP.NET Core-a (`:5180`) proxy edir → eyni origin, CORS lazım deyil. Lokal SQL Server instance (Windows Authentication).
- **Prod:** `client` build nəticəsi `ExamFlow.Api/wwwroot`-a kopyalanır, API SPA-nı statik fayl kimi verir (`MapFallbackToFile("index.html")`). Bir container + SQL Server / Azure SQL. Yüklənmiş PDF-lər üçün persistent volume.

## 5. Təhlükəsizlik

| Mövzu | Tədbir |
|---|---|
| Fayl validasiyası | Extension + `Content-Type` + magic bytes `%PDF-`; ölçü limiti Kestrel (`MaxRequestBodySize`) **və** endpoint səviyyəsində; şifrələnmiş PDF rədd edilir |
| Fayl saxlanması | Server tərəfindən yaradılmış ad (`{guid}.pdf`), `wwwroot`-dan kənarda; istifadəçinin fayl adı yalnız DB-də göstərmək üçün saxlanılır |
| Təhlükəsiz emal | Parsing ayrıca task-da, timeout (60 s) və səhifə limiti ilə; PdfPig JavaScript/embedded fayl icra etmir |
| Input validation | DTO-larda uzunluq limitləri (sual ≤ 4000, variant ≤ 1000 simvol), 2–6 variant, tam olaraq 0 və ya 1 düzgün variant |
| Authorization | Bütün route qrupları `RequireAuthorization()`; ownership yoxlaması servisdə; imtahan yalnız sahibi tərəfindən oxunur (başqası üçün 404) |
| Token | JWT `Authorization` header-də; brauzer onu avtomatik göndərmir → CSRF tətbiq olunmur. Signing key yalnız user-secrets / `Jwt__SigningKey` env-də (≥ 32 bayt, yoxdursa tətbiq başlamır). Frontend tokeni `localStorage`-da saxlayır (XSS riskinə qarşı: React escaping, `dangerouslySetInnerHTML` yoxdur) |
| Şifrə bərpası | Eyni cavab (hesab aşkarlanmır), email arxa planda göndərilir (cavab vaxtı da fərqlənmir), link 30 dəq + birdəfəlik, link URL-i yalnız konfiqurasiyadan (Host header injection yoxdur) |
| Rate limiting | Built-in `RateLimiter` per IP: login/register 10/dəq, forgot/reset password 5/15 dəq. Battle chat: istifadəçi başına 5 mesaj / 10 s, ≤ 300 simvol, idarəetmə simvolları silinir; mətn React-da escape olunur (HTML/script icra olunmur) |
| Xəta idarəsi | Global exception handler → ProblemDetails; stack trace yalnız Development-də |
| Şifrə | Identity default hashing, min 8 simvol; lockout 5 uğursuz cəhddən sonra |
| Məlumat sızması | İmtahan DTO-larında `IsCorrect` sahəsi yoxdur (ayrıca DTO tipi, test ilə təmin olunur) |

## 6. Cari implementasiya (necə işləyir)

### Sorğu axını
```
Brauzer (React, :5190) ──/api──▶ Vite proxy ──▶ ASP.NET Core (:5180)
  Authorization: Bearer <JWT>        ExceptionHandler → Authentication (JWT + SecurityStamp yoxlaması)
                                     → Authorization → RateLimiter → endpoint qrupu (/api/<modul>)
                                     → handler → AppDbContext (EF Core) → SQL Server
```

### Modullar və əsas axınlar
| Modul | Axın |
|---|---|
| Auth | Register/Login → Identity (PBKDF2 hash, lockout) → `TokenService` JWT (60 dəq, `sub`/`role`/`sst`). Logout və şifrə bərpası SecurityStamp-i dəyişir → köhnə tokenlər rədd olunur. Forgot password → `EmailQueue` → `SmtpEmailService` |
| Documents | Upload (validasiya, SHA-256 dublikat yoxlaması) → `IFileStorage` → `ProcessingQueue` → `DocumentProcessingService` → `PdfQuestionParser` → `Questions` + `AnswerOptions` (tək `SaveChanges`) |
| Questions | Sənəd üzrə səhifələnmiş read-only siyahı (redaktə Phase 4-dədir) |
| Battles | REST `POST /api/battles` otaq yaradır (host) → SignalR `/hubs/battle` (JWT `access_token` query, yalnız `/hubs` üçün). `BattleService` (singleton) otaqları yaddaşda saxlayır, hər otaq üçün `SemaphoreSlim`; oyun dövrü: sual (15–30 s) → reveal (6 s) → növbəti; hamı cavab verəndə və ya host “Advance” edəndə erkən keçid. Cavab vaxtı, təkrar cavab, üzvlük və host səlahiyyəti serverdə yoxlanılır; düzgün cavab yalnız reveal-da göndərilir. Xal: `BattleScoring` (düzgün 500–1000, yanlış 0). Sonda `BattleResults` cədvəlinə yazılır |
| Exams | PDF + orijinal nömrə aralığı → `ExamEngine.PickQuestions` (Fisher–Yates, hash üzrə dedup) → `ExamQuestions` snapshot (qarışdırılmış variantlar) → cavablar → `ExamEngine.Grade` (serverdə). Vaxtı bitmiş imtahan növbəti sorğuda avtomatik tamamlanır |

### Arxa plan servisləri (in-process)
- `DocumentProcessingService` — PDF emalı; restart-da yarımçıq sənədləri yenidən növbəyə qoyur.
- `SmtpEmailService` — email göndərilməsi (`Smtp:Host` və ya dev üçün `Smtp:PickupDirectory`).
- `BattleCleanupService` — hər dəqiqə boş / bitmiş / köhnə battle otaqlarını silir.

**Real-time (SignalR):** ASP.NET Core-a daxildir (NuGet yoxdur); frontend `@microsoft/signalr`. MVP bir serverdədir — Redis/backplane yoxdur, buna görə otaq vəziyyəti restart-da itir. Bir neçə instansa keçəndə Azure SignalR və ya Redis backplane + otaq vəziyyətinin paylaşılan yaddaşa köçürülməsi lazımdır.

### Konfiqurasiya
| Açar | Harada |
|---|---|
| `ConnectionStrings:Default`, `Jwt:SigningKey`, `Smtp:UserName/Password` | user-secrets (dev) / env (prod) — **heç vaxt** `appsettings`-də deyil |
| `Jwt:Issuer/Audience/AccessTokenMinutes`, `Documents:*`, `Exam:*`, `Storage:RootPath` | `appsettings.json` |
| `App:FrontendBaseUrl`, `Smtp:PickupDirectory` | `appsettings.Development.json` (prod-da env) |

### Migration-lar
`InitialCreate` (Identity, sənədlər, suallar) → `AddExams` → `AddExamQuestionRange` → `AddBattleResults`. Sxem: `docs/DATABASE_SCHEMA.md`.

### Agent iş axını
Claude Code və OpenAI Codex növbə ilə işləyir. Ortaq qaydalar `AGENTS.md`-dədir (`CLAUDE.md` onu import edir); vəziyyət, növbəti addımlar və yarımçıq işin ötürülməsi `TASKS.md`-dədir. Commit/push yalnız istifadəçinin təsdiqi ilə.
