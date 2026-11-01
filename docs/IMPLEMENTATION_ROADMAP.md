# Implementation Roadmap

Hər mərhələ istifadəçi təsdiqi ilə başlayır. Tələb ID-ləri: [REQUIREMENTS.md](REQUIREMENTS.md).

| Phase | Status |
|---|---|
| 1 — Planning | ✅ Tamamlandı |
| 2 — Project Setup | ✅ Tamamlandı |
| 3 — PDF Processing | 🟡 Kod hazırdır — real PDF nümunələri ilə yoxlama gözlənilir |
| 4 — Question Bank | ⏳ |
| 5 — Exam Engine | ⏳ |
| 6 — Frontend | ⏳ |
| 7 — Testing & Optimization | ⏳ |

---

## Phase 1 — Planning
- **Məqsəd:** tələblər, arxitektura, DB, API, UI planı.
- **Nəticə:** `docs/` altında 9 sənəd.
- **Acceptance:** sənədlər bir-biri ilə uyğundur, açıq qərarlar istifadəçi ilə razılaşdırılıb.

## Phase 2 — Project Setup
- **Məqsəd:** build olunan backend, işləyən frontend, DB bağlantısı.
- **Tapşırıqlar:**
  - `ExamFlow.sln`, `ExamFlow.Api` (feature qovluqları), `ExamFlow.Parsing` (boş class library)
  - EF Core SqlServer, `AppDbContext`, Identity entity-ləri (`AppUser`), connection string user-secrets-də
  - `/api/health` (DB yoxlaması ilə) — frontend ilə ilk əlaqə
  - ProblemDetails, global exception handler, OpenAPI (Development)
  - `client/`: Vite + React + TS + Tailwind v4, Router, TanStack Query, `api-client.ts`, AppLayout + boş səhifələr
  - Vite `/api` proxy
- **Asılılıqlar:** .NET 9 SDK, Node 24, SQL Server instance.
- **Acceptance:** `dotnet build` xətasız; `npm run dev` işləyir; Dashboard API health statusunu (DB daxil) göstərir; connection string repo-da yoxdur.
- **Bu mərhələdə edilmir:** domen entity-ləri, migration, auth endpoint-ləri.

## Phase 3 — PDF Processing
- **Məqsəd:** PDF → strukturlaşdırılmış suallar ([PDF_PROCESSING.md](PDF_PROCESSING.md)).
- **Tapşırıqlar:** domen entity-ləri + ilk migration (`UploadedDocuments`, `Questions`, `AnswerOptions`); auth (Identity cookie, rollar, Admin seed); upload endpoint + validasiya; `IFileStorage`; `ProcessingQueue` + `DocumentProcessingService`; PdfPig mətn/stil çıxarışı; segmenter; `StyleAnswerDetector`; dedup; parser unit testləri.
- **Asılılıqlar:** Phase 2; `samples/`-də 2–3 real PDF.
- **Acceptance:** nümunə PDF-lər `*.expected.json` ilə 100% uyğun; düzgün cavab qeyri-müəyyən olanda `Unverified`; DOC-1..6, PARSE-1..6.

## Phase 4 — Question Bank
- **Məqsəd:** sualların idarəsi.
- **Tapşırıqlar:** list (filtr, axtarış, səhifələmə), detail, update (variantlar + düzgün cavab + rowversion), delete, `/questions/stats`; ownership yoxlaması.
- **Asılılıqlar:** Phase 3.
- **Acceptance:** QB-1..4, AUTH-3; başqasının sualını redaktə → 403; concurrency konflikti → 409.

## Phase 5 — Exam Engine
- **Məqsəd:** imtahan yaratma, sessiya, qiymətləndirmə ([EXAM_ENGINE.md](EXAM_ENGINE.md)).
- **Tapşırıqlar:** `Exams`/`ExamQuestions` migration; generator (Fisher–Yates, dedup); sessiya + cavab endpoint-ləri; lazy expiry; grader; result, retake, history.
- **Asılılıqlar:** Phase 4.
- **Acceptance:** EX-1..9, RES-1..5; sessiya JSON-unda düzgün cavab yoxdur; 100k sual ilə yaratma < 500 ms.

## Phase 6 — Frontend
- **Məqsəd:** bütün səhifələr ([FRONTEND_DESIGN.md](FRONTEND_DESIGN.md)).
- **Tapşırıqlar:** auth səhifələri; Dashboard; Upload (progress + polling); Question Bank (filtr + edit sheet); Exam Setup; Exam Session (naviqasiya, taymer, optimistic save, klaviatura); Results.
- **Asılılıqlar:** Phase 3–5 API-ları (səhifələr API hazır olduqca əlavə edilə bilər).
- **Acceptance:** 360 px-də işləyir; reload-dan sonra imtahan bərpa olunur; klaviatura ilə tam imtahan verilə bilir.

## Phase 7 — Testing & Optimization
- **Məqsəd:** keyfiyyət və performans.
- **Tapşırıqlar:** integration testlər (Testcontainers.MsSql), rate limiting, təhlükəsizlik yoxlaması, sorğu planlarının yoxlanması, axtarış performansı (lazım olsa Full-Text), prod build (SPA `wwwroot`-da), .NET 10 LTS-ə keçid.
- **Acceptance:** bütün testlər yaşıl; NFR-1..6.
