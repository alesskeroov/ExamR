# Requirements

ID-lər digər sənədlərdə və roadmap-da istinad üçün istifadə olunur.

## Rollar

| Rol | İcazələr |
|---|---|
| **User** | PDF yükləmək; öz PDF-lərinin suallarını redaktə/silmək; bütün bankı görmək; imtahan vermək; öz nəticələrini görmək |
| **Admin** | User-in hər şeyi + istənilən PDF/sualı redaktə/silmək |

İlk Admin seed ilə (konfiqurasiyadan) yaradılır.

## Funksional tələblər

### Auth (AUTH)
- **AUTH-1** Email + şifrə ilə qeydiyyat, giriş, çıxış.
- **AUTH-2** Bütün API endpoint-ləri (auth istisna) autentifikasiya tələb edir.
- **AUTH-3** Redaktə hüququ: `document.UploadedByUserId == currentUser || Admin`.

### PDF yükləmə (DOC)
- **DOC-1** Yalnız `.pdf`, maks. 20 MB, maks. 300 səhifə (konfiqurasiya).
- **DOC-2** Fayl məzmunu yoxlanılır (magic bytes `%PDF-`), şifrələnmiş PDF rədd edilir.
- **DOC-3** Emal arxa planda gedir; status: `Pending → Processing → Completed | Failed`.
- **DOC-4** Emal hesabatı: tapılan sual sayı, avtomatik təyin olunmuş cavab sayı, Unverified sayı, atlanmış dublikatlar, xəbərdarlıqlar.
- **DOC-5** PDF-i silmək onun bütün suallarını silir (keçmiş imtahan nəticələri qorunur).
- **DOC-6** Uğursuz emal üçün "Yenidən emal et" əməliyyatı.

### Parsing (PARSE)
- **PARSE-1** Sual nömrəsi formatları: `Sual 1:`, `1.`, `1)`, `1-`, `№1`.
- **PARSE-2** Variant formatları: `A)`, `A.`, `a)`, `A -`; 2–6 variant.
- **PARSE-3** Çox sətirli sual və variant mətnləri birləşdirilir.
- **PARSE-4** Düzgün cavab qalın/rəngli şriftdən aşkarlanır; qeyri-müəyyən halda **heç vaxt uydurulmur** → `Unverified`.
- **PARSE-5** Eyni PDF daxilində dublikatlar atlanır; PDF-lər arası dublikatlar işarələnir.
- **PARSE-6** Strukturu pozulmuş suallar `HasParseWarning` bayrağı ilə saxlanılır.

### Sual bankı (QB)
- **QB-1** Səhifələnmiş siyahı; PDF, status, mətn axtarışı üzrə filtr.
- **QB-2** Sual mətnini, variantları (əlavə/sil/dəyiş), düzgün cavabı redaktə etmək.
- **QB-3** Düzgün cavab təyin edildikdə status `Verified` olur.
- **QB-4** Sualı silmək.

### İmtahan (EX)
- **EX-1** Sual sayını istifadəçi seçir (1–200); mənbə — konkret PDF və ya bazadakı bütün PDF-lər.
- **EX-1a** Cavab rejimi: düzgün cavab **hər sualdan sonra** (`Immediate`; cavab verildikdən sonra dəyişdirilə bilməz) və ya **imtahan bitdikdə** (`AtEnd`; cavablar sona qədər dəyişdirilə bilər).
- **EX-2** Suallar təsadüfi seçilir, imtahan daxilində təkrar yoxdur (dublikat hash nəzərə alınır).
- **EX-3** Kifayət qədər sual yoxdursa, imtahan yaradılmır və mövcud say göstərilir.
- **EX-4** Sual sırası və variant sırası imtahan üçün sabitdir.
- **EX-5** Cavab dəyişdirilə bilər; hər dəyişiklik dərhal serverdə saxlanılır.
- **EX-6** Səhifə yenilənəndə imtahan eyni vəziyyətdən davam edir.
- **EX-7** Vaxt limiti: 1 sual = 1 dəqiqə (30 → 30 dəq, 50 → 50 dəq; konfiqurasiya edilə bilər). Vaxt bitəndə imtahan avtomatik tamamlanır.
- **EX-8** İmtahan zamanı düzgün cavablar client-ə göndərilmir.
- **EX-9** İstifadəçinin eyni anda yalnız bir aktiv imtahanı ola bilər.

### Nəticələr (RES)
- **RES-1** Ümumi, düzgün, yanlış, cavabsız, qiymətləndirilməmiş (Unverified) saylar.
- **RES-2** Uğur faizi = düzgün / qiymətləndirilən suallar.
- **RES-3** Hər sual üzrə: seçilmiş cavab, düzgün cavab.
- **RES-4** "Yenidən imtahan ver" (eyni parametrlərlə yeni təsadüfi imtahan).
- **RES-5** İmtahan tarixçəsi (Dashboard).

## Qeyri-funksional tələblər

| ID | Tələb |
|---|---|
| NFR-1 | 100 000 suallıq bankda imtahan yaradılması < 500 ms |
| NFR-2 | Siyahı endpoint-ləri həmişə səhifələnir (maks. 100/səhifə) |
| NFR-3 | Bütün I/O async |
| NFR-4 | Responsive UI (≥ 360 px), WCAG 2.1 AA əsas tələbləri |
| NFR-5 | Xətalar RFC 9457 ProblemDetails formatında |
| NFR-6 | Parser unit testlərlə əhatə olunur (nümunə PDF fixture-ları) |

## Açıq məsələ

- **Nümunə PDF-lər lazımdır.** Bold/rəng aşkarlanması PDF-in daxili şrift quruluşundan asılıdır; Phase 3-dən əvvəl `samples/` qovluğuna 2–3 real PDF qoyulmalıdır.
