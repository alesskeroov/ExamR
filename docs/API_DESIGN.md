# API Design

Base path: `/api`. JSON, `camelCase`. Auth: Identity cookie (`HttpOnly`, `SameSite=Strict`). OpenAPI sənədi: `/openapi/v1.json` (yalnız Development).

## Ümumi qaydalar

- **Səhifələmə:** `?page=1&pageSize=20` (maks. 100). Cavab: `{ items, page, pageSize, totalCount }`.
- **Xətalar:** RFC 9457 ProblemDetails. Validation xətaları `400` + `errors: { field: [msg] }`.
- **Status kodları:** `200` OK, `201` Created (+`Location`), `202` Accepted (asinxron emal), `204` No Content, `400` validation, `401` giriş yox, `403` icazə yox (sahib deyil), `404` tapılmadı / başqasının imtahanı, `409` konflikt (dublikat fayl, aktiv imtahan, concurrency), `413` fayl böyükdür, `422` domen qaydası (kifayət qədər sual yox).
- Başqa istifadəçinin imtahanına müraciət **404** qaytarır (mövcudluğu açıqlamamaq üçün).

## Auth

| Method | Route | Təsvir |
|---|---|---|
| POST | `/api/auth/register` | `{ email, password, displayName }` |
| POST | `/api/auth/login` | `{ email, password }` → cookie set edilir |
| POST | `/api/auth/logout` | |
| GET | `/api/auth/me` | `{ id, email, displayName, roles[] }` |

Öz endpoint-lərimiz (`Features/Auth/AuthEndpoints.cs`) — `UserManager`/`SignInManager` + Identity cookie. `MapIdentityApi` istifadə olunmur, çünki `displayName` qəbul etmir. Yeni istifadəçi `User` rolunu alır; ilk Admin `Seed:Admin:Email` / `Seed:Admin:Password` konfiqurasiyasından (user-secrets) yaradılır.

## Documents

| Method | Route | Təsvir |
|---|---|---|
| POST | `/api/documents` | `multipart/form-data`, field `file` → `202` + `DocumentDto` (status `Pending`) |
| GET | `/api/documents` | Siyahı (səhifələnmiş), filtr: `?mine=true&status=` |
| GET | `/api/documents/{id}` | Status + `processingReport` (frontend emal zamanı 2 s-dən bir polling edir) |
| POST | `/api/documents/{id}/reprocess` | Sahib/Admin; yalnız `Failed` və ya `Completed` → mövcud suallar əvəz olunur |
| DELETE | `/api/documents/{id}` | Sahib/Admin → `204` |

```jsonc
// DocumentDto
{
  "id": "…", "originalFileName": "dotnet-test.pdf", "fileSizeBytes": 812345,
  "pageCount": 24, "status": "Completed",
  "uploadedBy": { "id": "…", "displayName": "Rəvan" }, "canEdit": true,
  "questionCount": 120,
  "processingReport": { "questionsFound": 120, "autoDetected": 112, "unverified": 8,
                        "duplicatesSkipped": 2, "warnings": ["Sual 47: yalnız 1 variant tapıldı"] },
  "createdAt": "…", "processedAt": "…"
}
```

## Questions

| Method | Route | Təsvir |
|---|---|---|
| GET | `/api/questions` | `?documentId=&status=&hasWarning=&search=&page=&pageSize=` |
| GET | `/api/questions/{id}` | Variantlar daxil |
| PUT | `/api/questions/{id}` | Tam yeniləmə (mətn + variantlar + düzgün cavab), sahib/Admin |
| DELETE | `/api/questions/{id}` | Sahib/Admin → `204` |
| GET | `/api/questions/stats?documentIds=a,b` | İmtahan setup üçün: `{ total, gradable, distinct }` |

```jsonc
// PUT /api/questions/{id}  — UpdateQuestionRequest
{
  "text": "ASP.NET Core nədir?",
  "options": [
    { "id": "…", "text": "Database sistemi" },
    { "id": null, "text": "Web framework" }        // id=null → yeni variant
  ],                                                // siyahıda olmayan mövcud variant silinir
  "correctOptionIndex": 1,                          // null → Unverified
  "version": "AAAAAAAAB9E="                         // rowversion (base64), optimistic concurrency → 409
}
```

Qaydalar: 2–6 variant, boş mətn yox, `correctOptionIndex` təyin olunubsa status → `Verified`, null-dursa → `Unverified`. Sıra (`Position`) massivdəki indeksdir. Uğurlu redaktədə `ContentHash` yenidən hesablanır.

Bank siyahısında hər sual `isCorrect` ilə qaytarılır (bank imtahan deyil); `canEdit` sahəsi UI üçün.

## Exams

| Method | Route | Təsvir |
|---|---|---|
| POST | `/api/exams` | `{ questionCount: 30\|50, documentIds: [] }` → `201` + `ExamSessionDto` |
| GET | `/api/exams` | İstifadəçinin imtahan tarixçəsi (səhifələnmiş, xülasə) |
| GET | `/api/exams/active` | Aktiv imtahan varsa `{ id }`, yoxdursa `204` |
| GET | `/api/exams/{id}` | Sessiya vəziyyəti (**düzgün cavab yoxdur**); vaxtı bitibsə avtomatik tamamlanır və `status: Completed` qaytarılır |
| PUT | `/api/exams/{id}/answers/{examQuestionId}` | `{ selectedOptionId: "…" \| null }` → `204`; vaxt bitibsə `409` |
| POST | `/api/exams/{id}/finish` | Qiymətləndirir → `ExamResultDto` (idempotent) |
| GET | `/api/exams/{id}/result` | Yalnız `Completed` imtahan üçün |
| POST | `/api/exams/{id}/retake` | Eyni `questionCount` + `documentIds` ilə yeni imtahan → `201` |

```jsonc
// ExamSessionDto
{
  "id": "…", "status": "InProgress", "questionCount": 30,
  "startedAt": "…", "expiresAt": "…", "serverNow": "…",   // client saat fərqini kompensasiya edir
  "questions": [
    { "examQuestionId": "…", "position": 0, "text": "…",
      "options": [ { "id": "…", "label": "A", "text": "…" } ],
      "selectedOptionId": null }
  ]
}

// ExamResultDto
{
  "id": "…", "questionCount": 30, "completionReason": "Submitted",
  "correct": 22, "incorrect": 5, "unanswered": 1, "ungraded": 2,
  "scorePercent": 78.57, "durationSeconds": 1820,
  "documentIds": ["…"],
  "questions": [
    { "position": 0, "text": "…", "options": [ … ],
      "selectedOptionId": "…", "correctOptionId": "…", "outcome": "Incorrect" }
  ]
}
```

Bütün sessiya (30–50 sual) bir sorğuda gəlir — sual başına sorğudan qaçmaq və Next/Prev keçidlərini ani etmək üçün (50 sual ≈ 30–60 KB).

## Rate limiting

| Policy | Limit | Tətbiq |
|---|---|---|
| `auth` | 10 / dəq / IP | login, register |
| `upload` | 10 / saat / user | `POST /api/documents` |
| `default` | 300 / dəq / user | qalan hamısı |
