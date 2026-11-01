# Exam Engine

Kod: `ExamFlow.Api/Features/Exams` — `ExamGenerator`, `ExamSessionService`, `ExamGrader`.
Cədvəllər: [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md#exams). Endpoint-lər: [API_DESIGN.md](API_DESIGN.md#exams).

## 1. Konfiqurasiya

```json
"Exam": {
  "Modes": { "30": 45, "50": 75 },     // sual sayı → dəqiqə
  "AnswerGraceSeconds": 5,             // şəbəkə gecikməsi üçün tolerans
  "MaxSourceDocuments": 20
}
```

## 2. İmtahan yaradılması

1. Validasiya: `questionCount ∈ {30, 50}`, `documentIds` 1..20, hamısı mövcud və `Completed`.
2. İstifadəçinin vaxtı bitmiş aktiv imtahanı varsa, avtomatik tamamlanır; vaxtı bitməmiş aktiv imtahan varsa → `409` (frontend "Davam et" təklif edir).
3. **Namizədlər (yalnız ID + hash):**
   ```sql
   SELECT q.Id, q.ContentHash FROM Questions q
   WHERE q.DocumentId IN (SELECT value FROM OPENJSON(@docIds))   -- EF Core 9: List.Contains tərcüməsi
     AND (SELECT COUNT(*) FROM AnswerOptions o WHERE o.QuestionId = q.Id) >= 2
   ```
   `AsNoTracking`, yalnız 2 sütun (16 + 32 bayt) → 10 000 sual ≈ 0.5 MB, bir neçə ms.
4. **Deduplikasiya:** əvvəl namizədlər qarışdırılır, sonra `ContentHash` üzrə ilk rast gələn saxlanılır (beləliklə hansı dublikatın seçildiyi də təsadüfidir).
5. Unikal namizəd sayı `< questionCount` → `422` + `{ available }`.
6. **Seçim: partial Fisher–Yates** — massivin yalnız ilk `N` elementi qarışdırılır, `O(N)` əlavə iş, bərabər paylanma. `Random.Shared` istifadə olunur (təhlükəsizlik baxımından kritik deyil).
7. Seçilmiş `N` sual variantları ilə **bir sorğuda** yüklənir (`ids.Contains(q.Id)` → `OPENJSON`, `Include(Options)`, `AsSplitQuery` lazım deyil — 50 × 6 sətir).
8. Hər sualın variantları da Fisher–Yates ilə qarışdırılır və yenidən `A, B, C…` label alır.
9. `Exams` + `ExamQuestions` (snapshot, `CorrectOptionId`) **tək `SaveChanges`** ilə yazılır. `ExpiresAt = now + müddət`.
10. Paralel sorğu yarışı `UX_Exams_UserActive` partial unique index ilə tutulur → `409`.

**Niyə `SELECT TOP (N) … ORDER BY NEWID()` deyil:** SQL Server bütün uyğun sətirlər üçün NEWID() yaradıb sıralamalıdır (scan + sort) və hash üzrə deduplikasiya ilə birləşdirmək çətindir. `TABLESAMPLE` səhifə səviyyəsində işləyir, dəqiq say vermir və filtr ilə zəifdir. ID-ləri tətbiqə gətirib qarışdırmaq gözlənilən miqyasda (≤ 100k) həm sadə, həm sürətlidir. Bank milyonlara çatsa, SQL-də `ROW_NUMBER() OVER (PARTITION BY ContentHash ORDER BY NEWID())` ilə deduplikasiya + `TOP (N)` yanaşmasına keçmək olar.

## 3. Sessiya

- **Sıra sabitdir:** `ExamQuestions.position` və snapshot-dakı variant sırası yaradılarkən təyin olunur və bir daha dəyişmir.
- **Düzgün cavab gizlidir:** `ExamSessionDto` snapshot-dan yalnız `text`, `options[{id,label,text}]` və `selectedOptionId` götürür. `CorrectOptionId` yalnız `Completed` imtahanın result DTO-sunda qaytarılır. Bunu integration test yoxlayır (JSON-da `correct` açarı olmamalıdır).
- **Cavab saxlanması** (`PUT /answers/{examQuestionId}`):
  - imtahan istifadəçiyə məxsusdur, `InProgress`-dir, `now ≤ ExpiresAt + grace`;
  - `selectedOptionId` həmin sualın snapshot variantlarındandır (və ya `null` — cavabı silmək);
  - tək `UPDATE` sorğusu, `AnsweredAt = now`.
- **Səhifə yeniləndikdə vəziyyət:** server yeganə həqiqət mənbəyidir. Hər cavab dərhal serverə yazılır; cari sual indeksi URL-dədir (`/exams/{id}?q=12`). Reload → `GET /api/exams/{id}` bütün suallar və seçilmiş cavablarla vəziyyəti tam bərpa edir. Brauzer bağlanıb yenidən açılsa da imtahan Dashboard-dakı "Davam et" ilə açılır. localStorage-a ehtiyac yoxdur.

## 4. Taymer

- `ExpiresAt` serverdə saxlanılır; client yalnız göstərir.
- Client saat fərqi: `offset = serverNow − Date.now()` (sessiya cavabından), qalan vaxt = `expiresAt − (Date.now() + offset)`.
- 0-a çatanda client `POST /finish` çağırır və nəticə səhifəsinə keçir.
- Client heç vaxt finish çağırmasa da (tab bağlanıb): növbəti `GET /exams/{id}`, `GET /exams/active`, `GET /exams` və ya yeni imtahan yaradılması zamanı vaxtı bitmiş imtahan **lazy** olaraq tamamlanır (`CompletionReason = TimeExpired`). Ayrıca background sweeper lazım deyil.
- Vaxt bitdikdən sonra gələn cavablar `409` ilə rədd edilir.

## 5. Qiymətləndirmə (`ExamGrader`)

Hər `exam_question` üçün:

| Şərt | Outcome |
|---|---|
| `CorrectOptionId IS NULL` (Unverified sual) | `Ungraded` |
| `SelectedOptionId IS NULL` | `Unanswered` |
| `selected = correct` | `Correct` |
| əks halda | `Incorrect` |

```
gradable     = total − ungraded
scorePercent = gradable > 0 ? round(correct / gradable × 100, 2) : null
```

- Cavabsız suallar **qiymətləndirilən** sayılır (faizi azaldır), Ungraded isə faizdə iştirak etmir.
- `AutoDetected` suallar ballanır (snapshot anında düzgün cavab var).
- Nəticə finish anında hesablanıb `Exams` və `ExamQuestions.Outcome`-a yazılır (tək transaction). Sonradan bankda sual düzəldilsə, keçmiş nəticə **dəyişmir** — snapshot prinsipi.
- `POST /finish` idempotentdir: artıq `Completed`-dirsə saxlanmış nəticəni qaytarır.

## 6. Yenidən imtahan

`POST /api/exams/{id}/retake` → eyni `QuestionCount` və `SourceDocumentIds` ilə §2 axını yenidən işləyir (yeni təsadüfi seçim). Silinmiş sənədlər siyahıdan çıxarılır; heç biri qalmayıbsa `422`.

## 7. Testlər

- Generator: təkrarsızlıq, dublikat hash-lərin birlikdə seçilməməsi, kifayət etməyən say → 422, eyni parametrlərlə iki imtahanın fərqli sırası (statistik).
- Fisher–Yates paylanması: 10 000 iterasiyada hər elementin mövqe tezliyi bərabərə yaxın (χ² testi).
- Grader: dörd outcome-un hamısı, gradable = 0 halı.
- Integration: sessiya DTO-sunda düzgün cavab yoxdur; vaxtı bitmiş imtahana cavab → 409; başqasının imtahanı → 404.
