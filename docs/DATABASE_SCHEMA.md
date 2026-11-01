# Database Schema (Microsoft SQL Server 2022)

Provider: `Microsoft.EntityFrameworkCore.SqlServer` (EF Core 9).

## Ümumi qaydalar

- **Primary key:** `uniqueidentifier`, EF Core-un `SequentialGuidValueGenerator`-u ilə (SQL Server-in Guid sıralamasına uyğun ardıcıl dəyərlər → clustered index fragmentasiyası azdır; URL-də ID təxmin edilə bilmir). `Guid.CreateVersion7()` **istifadə olunmur**: SQL Server `uniqueidentifier`-i son 6 baytdan başlayaraq sıralayır, v7-nin zaman hissəsi isə əvvəldədir.
- **Adlandırma:** SQL Server konvensiyası — `PascalCase`, cədvəllər cəm halında (`Questions`). Əlavə paket lazım deyil.
- **Vaxt:** `datetimeoffset(3)`, həmişə UTC (`DateTimeOffset.UtcNow`); `CreatedAt/UpdatedAt` `SaveChanges` interceptor ilə doldurulur.
- **Mətn:** `nvarchar` (Unicode — ə, ğ, ş, ç, ı). Axtarılan mətn sütunlarında collation **`Azeri_Latin_100_CI_AS`** (İ/ı düzgün case-insensitive müqayisə). Database default collation dəyişdirilmir.
- **Enum-lar:** `varchar(20)` string kimi (`HasConversion<string>()`).
- **Hash-lər:** SHA-256 `binary(32)` (hex string-dən 2 dəfə kiçik, indeks üçün daha səmərəli).
- **JSON:** `nvarchar(max)` + EF Core `ToJson()` (owned types) / primitive collection; `CHECK (ISJSON(...) = 1)`. SQL Server 2022-də native `json` tipi yoxdur.
- **Concurrency:** redaktə olunan cədvəllərdə `rowversion` sütunu (`[Timestamp] byte[] RowVersion`).

## ER diaqramı

```
AspNetUsers 1───* UploadedDocuments 1───* Questions 1───* AnswerOptions
     │                                       │
     │                                       │ (SET NULL)
     1───* Exams 1─────────────────────* ExamQuestions
```

## Cədvəllər

### Identity cədvəlləri
`AppUser : IdentityUser<Guid>` + `DisplayName nvarchar(100)`. Rollar: `Admin`, `User` (standart `AspNetRoles` / `AspNetUserRoles`).

### `UploadedDocuments`

| Sütun | Tip | Qeyd |
|---|---|---|
| Id | uniqueidentifier PK | |
| OriginalFileName | nvarchar(255) | yalnız göstərmək üçün |
| StoredFileName | varchar(64) | `{guid}.pdf` |
| FileSizeBytes | bigint | |
| Sha256 | binary(32) | **unique** — eyni faylın təkrar yüklənməsinin qarşısını alır (409) |
| PageCount | int null | |
| Status | varchar(20) | `Pending/Processing/Completed/Failed` |
| ProcessingReport | nvarchar(max) null, JSON | `{questionsFound, autoDetected, unverified, duplicatesSkipped, warnings[]}` |
| ErrorMessage | nvarchar(1000) null | |
| UploadedByUserId | uniqueidentifier FK → AspNetUsers | `NO ACTION` (Restrict) |
| CreatedAt, UpdatedAt, ProcessedAt | datetimeoffset(3) | |

İndekslər: `UX_UploadedDocuments_Sha256`, `IX_UploadedDocuments_UploadedByUserId`, `IX_UploadedDocuments_Status`.

### `Questions`

| Sütun | Tip | Qeyd |
|---|---|---|
| Id | uniqueidentifier PK | |
| DocumentId | uniqueidentifier FK → UploadedDocuments | `CASCADE` |
| SourceNumber | int | PDF-dəki sual nömrəsi |
| Text | nvarchar(4000), `Azeri_Latin_100_CI_AS` | |
| ContentHash | binary(32) | normallaşdırılmış mətn + variantların SHA-256-sı (dublikat aşkarlanması) |
| VerificationStatus | varchar(20) | `Unverified / AutoDetected / Verified` |
| ParseWarning | nvarchar(500) null | null deyilsə UI "yoxla" nişanı göstərir |
| CreatedAt, UpdatedAt | datetimeoffset(3) | |
| RowVersion | rowversion | concurrency token |

İndekslər:
- `IX_Questions_DocumentId_SourceNumber` (DocumentId, SourceNumber) INCLUDE (VerificationStatus)
- `IX_Questions_ContentHash` — PDF-lər arası dublikatlar və imtahanda deduplikasiya

**Axtarış:** `EF.Functions.Like(q.Text, "%term%")` (parametrləşdirilmiş, `%`, `_`, `[` escape olunur). Leading wildcard indeks istifadə etmir → sənəd filtri + səhifələmə ilə birlikdə 100k sətirdə qəbul ediləndir. Ölçmə yavaşlıq göstərsə: **Full-Text Search** (neutral language, LCID 0 — Azərbaycan dili üçün word breaker yoxdur). Qeyd: hazırkı dev instance-da Full-Text komponenti quraşdırılmayıb.

**Verification status semantikası:**
- `Unverified` — düzgün variant yoxdur → imtahanda ballanmır.
- `AutoDetected` — parser bold/rəngdən təyin edib, insan təsdiqləməyib → **ballanır**, bankda "Avtomatik" nişanı.
- `Verified` — sahib/Admin təsdiqləyib.

### `AnswerOptions`

| Sütun | Tip | Qeyd |
|---|---|---|
| Id | uniqueidentifier PK | |
| QuestionId | uniqueidentifier FK → Questions | `CASCADE` |
| Position | tinyint | 0..5 |
| Label | char(1) | `A..F` |
| Text | nvarchar(1000), `Azeri_Latin_100_CI_AS` | |
| IsCorrect | bit | |

Constraint-lər:
- `UX_AnswerOptions_QuestionId_Position` UNIQUE (QuestionId, Position)
- `UX_AnswerOptions_SingleCorrect` UNIQUE (QuestionId) **WHERE [IsCorrect] = 1** (filtered index) — bir sualda maksimum 1 düzgün cavab DB səviyyəsində təmin olunur
- `CK_AnswerOptions_Position` CHECK (Position BETWEEN 0 AND 5)

### `Exams`

| Sütun | Tip | Qeyd |
|---|---|---|
| Id | uniqueidentifier PK | |
| UserId | uniqueidentifier FK → AspNetUsers | `CASCADE` |
| QuestionCount | smallint | CHECK BETWEEN 1 AND 200 |
| RevealMode | varchar(20) | `Immediate / AtEnd` |
| RangeFrom, RangeTo | int null | PDF-dəki orijinal sual nömrələri aralığı (`Questions.SourceNumber`); köhnə imtahanlarda null |
| SourceDocumentIds | nvarchar(max), JSON array | Bir PDF (`[id]`); köhnə "bütün PDF-lər" imtahanlarında boş; "yenidən ver" üçün; FK deyil (sənəd silinə bilər) |
| Status | varchar(20) | `InProgress / Completed` |
| CompletionReason | varchar(20) null | `Submitted / TimeExpired` |
| StartedAt, ExpiresAt, CompletedAt | datetimeoffset(3) | |
| CorrectCount, IncorrectCount, UnansweredCount, UngradedCount | smallint null | finish zamanı yazılır |
| ScorePercent | decimal(5,2) null | |

İndekslər:
- `IX_Exams_UserId_StartedAt` (UserId, StartedAt DESC) — tarixçə
- `UX_Exams_UserActive` UNIQUE (UserId) **WHERE [Status] = 'InProgress'** (filtered index) — bir aktiv imtahan qaydası (EX-9)

### `ExamQuestions`

| Sütun | Tip | Qeyd |
|---|---|---|
| Id | uniqueidentifier PK | |
| ExamId | uniqueidentifier FK → Exams | `CASCADE` |
| QuestionId | uniqueidentifier null FK → Questions | `SET NULL` |
| Position | tinyint | imtahandakı sıra (0-based) |
| Snapshot | nvarchar(max), JSON | `{ text, options: [{ id, label, text }] }` — **qarışdırılmış** variant sırası |
| CorrectOptionId | uniqueidentifier null | snapshot anındakı düzgün variant; null → ballanmır |
| SelectedOptionId | uniqueidentifier null | istifadəçinin cavabı |
| AnsweredAt | datetimeoffset(3) null | |
| Outcome | varchar(20) null | `Correct / Incorrect / Unanswered / Ungraded` (finish zamanı) |

İndeks: `UX_ExamQuestions_ExamId_Position` UNIQUE (ExamId, Position).

## Model qərarları

**Question və AnswerOption ayrı cədvəllərdə (JSON massiv yerinə):**
- Dəyişkən variant sayı (2–6) normal formada saxlanılır.
- "Tək düzgün cavab" filtered unique index ilə DB səviyyəsində təmin olunur — JSON-da mümkün deyil.
- Variant ID-ləri sabitdir → snapshot və `SelectedOptionId` onlara etibarlı istinad edir.
- Ayrı variantı redaktə etmək/silmək sadədir.
- Mənfi tərəfi (join) projection və `Include` ilə əhəmiyyətsizdir.

**`ExamAnswer` və `ExamResult` ayrıca cədvəl deyil:**
- Hər imtahan sualına tam olaraq bir cavab düşür (1:1) → cavab `ExamQuestions`-da sütundur.
- Hər imtahanın bir nəticəsi var (1:1) → nəticə sayları `Exams`-dadır.

**`ExamQuestions.Snapshot` (immutable surət):**
- İmtahan yaradılan anda sual mətni və qarışdırılmış variantlar köçürülür.
- Sonradan bankda sual redaktə və ya silinsə də, keçmiş imtahanlar və nəticələr dəyişmir.
- İmtahan sessiyası tək cədvəldən oxunur (bank cədvəllərinə join yoxdur).
- `CorrectOptionId` ayrıca sütundadır və sessiya DTO-suna heç vaxt daxil edilmir.

## Cascade davranışı

| Silinən | Nəticə |
|---|---|
| Sənəd | Sualları və variantları silinir; `ExamQuestions.QuestionId` → NULL; keçmiş nəticələr qalır |
| Sual | Variantları silinir; `ExamQuestions.QuestionId` → NULL |
| İstifadəçi | MVP-də istifadəçi silinmir; sənədlər üzrə `NO ACTION` |
| İmtahan | `ExamQuestions` silinir |

**SQL Server "multiple cascade paths" məhdudiyyəti:** `ExamQuestions`-a iki yol gəlir (`Exams → CASCADE`, `Questions → SET NULL`). Ümumi cascade əcdadı yoxdur, çünki `AspNetUsers → UploadedDocuments` `NO ACTION`-dır — ona görə sxem qəbul olunur. Gələcəkdə bu FK dəyişsə və migration xəta versə, `ExamQuestions.QuestionId` `ClientSetNull`-a keçirilir və sual silinərkən servis `ExecuteUpdateAsync` ilə NULL yazır.

## EF Core konfiqurasiyası

- Hər entity üçün `IEntityTypeConfiguration<T>` (`Infrastructure/Data/Configurations/`), `ApplyConfigurationsFromAssembly`.
- Filtered index-lər: `HasIndex(...).IsUnique().HasFilter("[IsCorrect] = 1")`.
- Collation: `Property(x => x.Text).UseCollation("Azeri_Latin_100_CI_AS")`.
- `Snapshot` və `ProcessingReport`: owned type + `.ToJson()`.
- `SourceDocumentIds`: `List<Guid>` → EF Core primitive collection (JSON).
- `Contains` sorğuları EF Core 9-da `OPENJSON` parametrinə çevrilir (çoxlu parametr / plan cache problemi yoxdur).
- `EnableRetryOnFailure()` (transient xətalar, Azure SQL üçün vacib).
- Oxuma sorğularında `AsNoTracking()` və DTO projection.

## Migration strategiyası

- Code First; migration-lar `ExamFlow.Api/Infrastructure/Data/Migrations`.
- Hər məntiqi dəyişiklik üçün bir migration, mənalı ad (`AddQuestionSearchIndex`).
- **Dev:** `dotnet ef database update` (connection string user-secrets-dən).
- **Prod:** `dotnet ef migrations script --idempotent` ilə SQL script yaradılır və deploy zamanı tətbiq edilir. Startup-da `Database.Migrate()` **istifadə olunmur**.
- Seed: rollar və ilk Admin — konfiqurasiyadan oxuyan idempotent startup seeder.

## Connection string

| Mühit | Mənbə | Nümunə |
|---|---|---|
| Development | `dotnet user-secrets` (`ConnectionStrings:Default`) | `Server=localhost;Database=ExamFlow;Trusted_Connection=True;TrustServerCertificate=True` |
| Production | Environment variable `ConnectionStrings__Default` / secret store | SQL auth və ya Managed Identity, `Encrypt=True`, `TrustServerCertificate=False` |

`appsettings.json`-da connection string saxlanılmır.
