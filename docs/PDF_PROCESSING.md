# PDF Processing

Kitabxana: **PdfPig** (NuGet: `PdfPig`) (seçim səbəbi: [ARCHITECTURE.md](ARCHITECTURE.md#pdf-kitabxanası-müqayisəsi)).
Kod: `ExamFlow.Parsing` (təmiz kitabxana) + `Features/Documents` (upload, növbə, persist).

## 1. Emal axını

```
POST /api/documents
  │ validate (ölçü, extension, magic bytes, sha256 unikal)
  │ IFileStorage.Save → {guid}.pdf
  │ INSERT UploadedDocuments (Pending)
  │ ProcessingQueue.Enqueue(id)            → 202 Accepted
  ▼
DocumentProcessingService (BackgroundService, Channel<Guid>)
  │ status = Processing
  │ PdfQuestionParser.Parse(stream, ct)   ← Task.Run, timeout 60 s
  │ dedup (PDF daxili skip, PDF-lər arası işarə)
  │ INSERT questions + AnswerOptions     ← tək transaction, tək SaveChanges
  │ status = Completed + ProcessingReport
  └─ xəta → status = Failed + ErrorMessage (istifadəçi üçün oxunaqlı)
```

- Startup-da `Pending/Processing` statuslu sənədlər yenidən növbəyə qoyulur (restart zamanı itməmək üçün).
- Paralellik: `MaxConcurrentJobs = 2` (konfiqurasiya). CPU-bound iş request thread-lərini bloklamır.

## 2. Parser pipeline (`ExamFlow.Parsing`)

```
IPdfTextSource      → Page[] → Line[] (mətn + hər hərfin stil məlumatı)
  ↓
TextNormalizer      → Unicode NFC, ligature-lar (ﬁ→fi), defis-sətirsonu birləşmə, boşluqlar
  ↓
LayoutCleaner       → header/footer (səhifələrin ≥50%-ində eyni y-də təkrarlanan sətirlər), səhifə nömrələri
  ↓
QuestionSegmenter   → state machine: Seeking → InQuestion → InOption
  ↓
IAnswerDetector     → StyleAnswerDetector (MVP)
  ↓
QuestionValidator   → xəbərdarlıqlar
  ↓
ParseResult { Questions[], Warnings[], Stats }
```

### 2.1 Mətn çıxarışı
- `PdfDocument.Open(stream, new ParsingOptions { UseLenientParsing = true })`.
- Sözlər `NearestNeighbourWordExtractor` ilə, sətirlər baseline Y koordinatına görə qruplaşdırılır (tolerans = şrift ölçüsünün 30%-i).
- Hər `Line` saxlayır: `Text`, `Spans[] { Text, IsBold, ColorKey, FontSize }`.
- `IsBold` = `letter.FontDetails.IsBold` **və ya** `FontName` daxilində `Bold|Black|Heavy|Semibold|Demi` **və ya** `RenderingMode = FillThenStroke` (stroke ilə "qalınlaşdırılmış" mətn). Eyni hərfin sürüşmə ilə iki dəfə çəkilməsi ilə yaradılan "fake bold" hələ dəstəklənmir — real nümunələrdə rast gəlinsə əlavə ediləcək.
- `ColorKey` = hərfin fill rəngi (RGB-yə çevrilir; qara/tünd boz ≈ "default").

### 2.2 Segmentasiya qaydaları

| Element | Regex (sətrin əvvəli) |
|---|---|
| Sual başlanğıcı | `^(?:Sual\s*|№\s*)?(\d{1,4})\s*[.):\-]\s*(.*)$` (case-insensitive) |
| Variant | `^([A-Fa-f])\s*[).]\s*(.+)$` |
| Eyni sətirdə bir neçə variant | `A) x   B) y` → `(?=\s[A-F]\)\s)` üzrə bölünür |

- Sual nömrəsi **ardıcıllıq yoxlanışı** ilə qəbul edilir: gözlənilən `last + 1`-dir. Sual mətni daxilində "2. addım" kimi sətirlər, əgər hələ variant görünməyibsə, nömrə ardıcıl deyilsə, sual mətninə birləşdirilir.
- Nömrə sıçrayışı (məs. 14 → 16) qəbul edilir, amma report-a xəbərdarlıq yazılır.
- Variant hərfləri ardıcıl olmalıdır (A, B, C…). `A` gəlmədən `B` görünərsə — sual mətninin davamı sayılır.
- **Marker variantları:** sətir `• ● ○ ◦ ▪ ■ □` və ya `√ ✓ ✔ ☑` ilə başlayırsa — yeni variant; hərflər (A, B, C…) ardıcıllıqla verilir. Belə variantların mətnində "B)" bölünmür.
- **Mərkəzləşdirilmiş cədvəl xanaları** (məs. UNEC formatı): sual nömrəsi və marker çox sətirli xananın şaquli ortasında olur, ona görə xananın bir hissəsi marker sətrindən **yuxarıda** çıxarılır. Nömrə/marker olmayan hər sətir Y koordinatına görə **ən yaxın** anchor-a (sual nömrəsi və ya marker) bağlanır; bərabərlikdə yuxarıdakına qalır. Aşağıdakı anchor-a bağlanmaq üçün məsafə ≤ 12pt olmalıdır (bir xana sətri) — beləliklə səhifə başlığı birinci suala qoşulmur.
- Variant və ya sual mətnindən sonra gələn, heç bir pattern-ə uyğun olmayan sətirlər əvvəlki elementə birləşdirilir (çox sətirli mətn).

### 2.3 Düzgün cavabın aşkarlanması

Detektorlar ardıcıl işləyir, ilk əmin nəticə qəbul olunur:

1. **`MarkerAnswerDetector`** — variant adi bullet əvəzinə `√ ✓ ✔ ☑` ilə başlayırsa, düzgün cavab odur. Birdən çox işarə → `Unverified` + xəbərdarlıq.
2. **`StyleAnswerDetector`** — aşağıdakı vizual qaydalar.

#### `StyleAnswerDetector`

Hər variant üçün (label daxil və label xaric ayrıca) hesablanır:
- `boldRatio` = qalın hərflərin payı
- `dominantColor` = ən çox rast gələn `ColorKey`

**Baseline:** sual mətninin və variantların əksəriyyətinin stili.

Qərar:
1. Tam olaraq **bir** variantda `boldRatio ≥ 0.6`, qalanlarında `≤ 0.2` → həmin variant düzgündür.
2. Əks halda, tam olaraq **bir** variantın `dominantColor`-u baseline-dan fərqlidir → həmin variant.
3. Bütün variantlar qalındırsa (bütün sənəd bold-dursa) → stil siqnalı yoxdur.
4. 0 və ya >1 namizəd → `CorrectIndex = null` → **`Unverified`**; >1 halında xəbərdarlıq.

Nəticə: tapılıbsa `AutoDetected`, tapılmayıbsa `Unverified`. **Heç vaxt təxmin edilmir.**

Genişlənmə: `IAnswerDetector` interfeysi sayəsində sonradan `TextMarkerDetector` (`Cavab: B`, `*`) və `AnswerKeyDetector` (sonda `1-B, 2-C` cədvəli) əlavə edilə bilər.

### 2.4 Real nümunələr (`samples/`)

| Fayl | Format | Nəticə |
|---|---|---|
| `unec-sample.pdf` | 91 səhifə, cədvəl, `•` variantlar, `√` düzgün cavab, mərkəzləşdirilmiş xanalar | 669/669 sual, 669 düzgün cavab, 0 xəbərdarlıq (`RealSampleTests`) |

### 2.5 Validasiya və xəbərdarlıqlar

| Hal | Davranış |
|---|---|
| < 2 variant | Saxlanılır, `ParseWarning`, imtahana **düşmür** |
| > 6 variant | İlk 6 saxlanılır + xəbərdarlıq |
| Boş sual mətni | Atlanır, report-a yazılır |
| Mətn > 4000 simvol | Kəsilir + xəbərdarlıq (yəqin segmentasiya xətası) |
| Çoxlu `�` / tanınmayan qliflər (>5%) | Sənəd səviyyəsində xəbərdarlıq: "şrift kodlaşdırması problemi" |

### 2.6 Dublikat aşkarlanması

`ContentHash = SHA-256( norm(questionText) + "\n" + join("\n", sort(norm(optionText))) )`
`norm` = `az-Latn-AZ` kulturası ilə lowercase (İ/ı düzgün), nömrə/label silinir, boşluqlar sıxılır, sondakı durğu işarələri silinir.

- **Eyni PDF daxilində:** dublikat atlanır, `duplicatesSkipped++`.
- **PDF-lər arası:** saxlanılır (hər PDF öz suallarını tam göstərsin), bankda "Dublikat" nişanı; imtahan yaradılarkən hash üzrə deduplikasiya edilir ([EXAM_ENGINE.md](EXAM_ENGINE.md)).

## 3. Xəta idarəsi

| Xəta | Status | İstifadəçiyə mesaj |
|---|---|---|
| Şifrələnmiş PDF | Upload zamanı 400 | "Şifrə ilə qorunan PDF dəstəklənmir" |
| Pozulmuş fayl (PdfPig exception) | Failed | "PDF faylı oxuna bilmədi" |
| Mətn yoxdur (səhifə başına < 20 simvol, şəkillər var) | Failed | "Skan edilmiş PDF — OCR hələ dəstəklənmir" |
| 0 sual tapıldı | Failed | "Sual formatı tanınmadı" + nümunə format |
| Timeout (60 s) | Failed | "Emal çox uzun çəkdi" |
| Səhifə limiti (> 300) | Upload zamanı 400 | |

Texniki detallar log-a (`documentId` ilə), istifadəçiyə yalnız oxunaqlı mesaj.

## 4. Testlər

- `samples/` qovluğundakı real PDF-lər fixture kimi; hər biri üçün gözlənilən nəticə JSON-u (`*.expected.json`): sual sayı, hər sualın variant sayı və düzgün indeksi.
- Segmenter və detector-lar `Line[]` girişi ilə ayrıca unit test olunur (PDF olmadan, sürətli).
- Kənar hallar: çox sətirli variant, eyni sətirdə 4 variant, səhifə arası bölünən sual, bütün mətn bold, iki bold variant.

## 5. Gələcək: OCR

- `IPdfTextSource`-un ikinci implementasiyası: `OcrTextSource` (Tesseract `aze` modeli və ya Azure Document Intelligence).
- Skan aşkarlandıqda avtomatik OCR-a yönləndirmə.
- OCR stil (bold) məlumatı vermir → bu suallar default olaraq `Unverified`.
- OCR ağır olduğu üçün o zaman ayrıca worker prosesə çıxarılması nəzərdən keçirilə bilər.
