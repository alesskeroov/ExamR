# DİM sınaqları (Buraxılış və Blok)

Abituriyentlər üçün DİM formatlı sınaqlar. İlk versiya: yalnız Azərbaycan bölməsi, yalnız qapalı (A–E) suallar.

## Struktur və qiymətləndirmə (`Features/Dim/DimCatalog.cs`)

Qaydalar data kimi saxlanılır; DİM dəyişiklik edərsə yalnız kataloq yenilənir. Mənbə: DİM-in 2025 qəbul elanı və
ictimai bal kalkulyatorları — rəsmi "Balların hesablanma qaydası" sənədi ilə yenidən yoxlanmalıdır.

| İmtahan | Fənn | Qapalı | Kodlaşdırılan | Yazılı | Bal |
|---|---|---|---|---|---|
| Buraxılış XI | Azərbaycan dili | 20 | 0 | 10 | 100 |
| Buraxılış XI | Riyaziyyat | 13 | 5 | 7 | 100 |
| Buraxılış XI | Xarici dil | 23 | 0 | 7 | 100 |
| Blok (hər fənn) | — | 22 | 5 | 3 | 100 × əmsal |

Fənn balı = `100 / (qapalı + kodlaşdırılan + 2 × yazılı)` × düzgün tapşırıq balı (yazılı ×2). Blokda qapalı səhv
düzgünün ¼-ni silir. Blok qrupları (əmsal 1,5 / 1,5 / 1): I-RK riy-fiz-kim, I-Rİ riy-fiz-inf, II riy-coğ-tar,
III-DT az-tar-ədb, III-TC az-tar-coğ, IV bio-kim-fiz. Maks: Buraxılış 300, Blok 400.

**Yalnız qapalı suallar** olduğu üçün tam bal hesablanmır. Hesabat iki rəqəm verir:
- *Qapalı hissə balı* — qapalı tapşırıqların rəsmi düsturla gətirdiyi bal (məs. riyaziyyat buraxılışda maks. 40,6);
- *Təxmini bal* — açıq hissədə eyni nəticə fərziyyəsi ilə 100 × əmsal üzərindən.
Sınaqdakı qapalı sual sayı rəsmi saydan fərqlənərsə, nəticə rəsmi saya miqyaslanır.

## Model

- `DimBankQuestions` — mövcud `Questions` sətrinə bağlı təsnifat (Track, Subject, Topic). Sual/variant/cavab
  `Questions`/`AnswerOptions`-da qalır; açıq suallar gələcəkdə ayrıca tip kimi əlavə oluna bilər.
- `DimTests` — sınaq: Track, Kind (Full/Subject), GroupCode, müddət, cəhd limiti, vaxt pəncərəsi, dərc,
  `Sections` (jsonb: fənn + sabit `QuestionIds` və ya hər cəhd üçün `RandomCount`).
- `DimAttempts` — cəhd = adi `Exam` (AtEnd rejimi, `SourceDocumentIds` boş) + fənn/mövzu xəritəsi. İmtahan ekranı,
  davam etmə (internet kəsilsə), server vaxtı, yoxlama və nəticə səhifəsi `Exams` modulundan istifadə edir.

## Axın

1. Admin PDF/DOCX-i adi `/api/documents` ilə yükləyir (parser sualları çıxarır, mümkünsə cavabı tanıyır).
2. Səhv suallar: `POST /api/admin/dim/documents/{id}/answer-key` (`1-A 2-C …`) və ya tək sual üçün
   `PUT /api/admin/dim/questions/{id}` (mətn, variant mətnləri, düzgün variant; variant sayı dəyişmir).
3. `POST /api/admin/dim/documents/{id}/approve-valid` — sənədin bütün düzgün formatlı suallarını (2–5 variant, tam 1
   düzgün, boş mətn yoxdur) fənn/mövzu ilə banka əlavə edir; səhvlər `rejected`-də qayıdır. Tək-tək: `POST /api/admin/dim/bank`.
4. `POST /api/admin/dim/tests` — hazır (sabit suallar), bir dəfə təsadüfi və ya hər cəhddə təsadüfi sınaq. Bölmədə
   `sourceDocumentId` verilsə, suallar yalnız həmin sənədin bankdakı suallarından seçilir (jsonb, migration yoxdur).
5. İstifadəçi: `GET /api/dim/tests?track=Buraxilis|Blok` → `POST /api/dim/tests/{id}/start` → `/exams/{examId}`
   → bitirmə → `GET /api/dim/attempts/{examId}/report` (fənlər, DİM balı, zəif mövzular; yalnız bitmiş imtahan).

Admin endpoint-ləri: `GET documents` (sənədlər + düzgün/bankda sayları), `GET documents/{id}/questions`, `DELETE bank/{questionId}`, `GET bank/stats`,
`GET|POST tests`, `PUT tests/{id}` (parametrlər/dərc), `DELETE tests/{id}` (sınağı və bütün cəhdlərini — istifadəçi nəticələri daxil — bir tranzaksiyada silir).

## UI

- İstifadəçi: sol menyu → "DİM sınaqları" → `/dim/buraxilis`, `/dim/blok` (tam/fənn filtri, Blokda qrup filtri;
  Başla / Davam et / Son nəticə). Nəticə səhifəsində DİM kartı (`features/dim/DimReportCard.tsx`).
- Admin: Admin Panel → "DİM sınaqları" (`?section=dim`):
  - *Sual bankı* — əvvəl imtahan+fənn seç, PDF yüklə; sənəd kartları (X düzgün / Y səhv / Z bankda); bir düymə ilə
    bütün düzgün sualları banka əlavə et; "Səhv suallar" siyahısında sualı yerində düzəlt; cavab açarı yalnız cavabı
    tapılmayan sual olanda görünür; bankdan çıxar;
  - *Sınaqlar* — "Yeni sınaq": bankdakı sənədi seç, ad sənəd adından dolur; yalnız müddət, sual sayı, cəhd limiti;
    hər cəhddə həmin sənəddən təsadüfi suallar; qaralama kimi yaranır → "Dərc et". İkinci yol: "Tam DİM sınağı
    (bütün bankdan)". Redaktə: ad, müddət, limit (vaxt pəncərəsi UI-da yoxdur, saxlanılır). Sil: cəhdlər də silinir.
- Hələ yoxdur: admin UI-da sualları tək-tək seçərək sınaq (API dəstəkləyir), açıq suallar, rus bölməsi.
