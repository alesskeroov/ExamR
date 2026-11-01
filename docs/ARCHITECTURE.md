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
- Repository qatı yoxdur → servislər `DbContext`-i birbaşa istifadə edir. İnteqrasiya testləri real SQL Server (Testcontainers) ilə aparılır, ona görə mock-a ehtiyac yoxdur.

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
| Test | xUnit, Testcontainers.MsSql, `WebApplicationFactory` | Real DB ilə integration test |
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
├─ src/
│  ├─ ExamFlow.Api/
│  │  ├─ Features/
│  │  │  ├─ Auth/            (Identity endpoint-ləri, /me)
│  │  │  ├─ Documents/       (upload, list, delete, DocumentProcessor, ProcessingQueue)
│  │  │  ├─ Questions/       (bank CRUD, search)
│  │  │  └─ Exams/           (create, session, answers, finish, results, ExamGenerator, ExamGrader)
│  │  ├─ Infrastructure/
│  │  │  ├─ Data/            (AppDbContext, Configurations/, Migrations/)
│  │  │  ├─ Storage/         (IFileStorage, LocalFileStorage)
│  │  │  └─ Security/        (CurrentUser, ownership helpers)
│  │  ├─ Common/             (ProblemDetails, Paging, Result types)
│  │  └─ Program.cs
│  └─ ExamFlow.Parsing/      (PdfPig, heç bir EF/HTTP asılılığı yox)
├─ tests/
│  ├─ ExamFlow.Parsing.Tests/
│  └─ ExamFlow.Api.Tests/    (integration, Testcontainers)
├─ client/                   (React + Vite)
├─ samples/                  (test üçün nümunə PDF-lər)
├─ ExamFlow.sln
└─ docs/
```

Hər feature qovluğu: `XxxEndpoints.cs` (route-lar), `XxxService.cs` (məntiq), `XxxDtos.cs` (request/response record-ları).

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
| Rate limiting | Built-in `RateLimiter` per IP: login/register 10/dəq, forgot/reset password 5/15 dəq |
| Xəta idarəsi | Global exception handler → ProblemDetails; stack trace yalnız Development-də |
| Şifrə | Identity default hashing, min 8 simvol; lockout 5 uğursuz cəhddən sonra |
| Məlumat sızması | İmtahan DTO-larında `IsCorrect` sahəsi yoxdur (ayrıca DTO tipi, test ilə təmin olunur) |
