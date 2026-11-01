# Frontend Design

## 1. Stack və asılılıqlar

| Ehtiyac | Seçim | Səbəb |
|---|---|---|
| UI komponentlər | **shadcn/ui** (Radix primitivləri) | Kod layihəyə kopyalanır, Tailwind ilə tam nəzarət, a11y Radix-dən gəlir, runtime kitabxana deyil |
| İkonlar | lucide-react | shadcn standartı |
| Routing | **React Router v7** (library mode) | Sadə SPA, framework mode lazım deyil |
| Server state | **TanStack Query v5** | Cache, polling (emal statusu), optimistic update (imtahan cavabları), retry |
| Client state | React `useState`/context | Qlobal state yoxdur → Redux/Zustand lazım deyil |
| Formalar | **React Hook Form + Zod** | Dinamik variant siyahısı (`useFieldArray`), performans, tip təhlükəsizliyi |
| Toast | sonner | shadcn standartı |
| HTTP | `fetch` wrapper (`lib/api-client.ts`) | axios lazım deyil; upload progress üçün yalnız orada `XMLHttpRequest` |
| API tipləri | `openapi-typescript` (dev dependency) | Backend OpenAPI-dən tiplər generasiya olunur → DTO uyğunsuzluğu olmur |
| Şrift | Inter (`@fontsource-variable/inter`) | Self-hosted, professional SaaS görünüşü |

Lazım olmayanlar: Redux, Zustand, axios, moment/date-fns (`Intl` kifayətdir), react-dropzone (native drag & drop), TanStack Table.

## 2. Qovluq strukturu

```
client/src/
├─ app/            router.tsx, providers.tsx, AppLayout.tsx, ExamLayout.tsx, RequireAuth.tsx
├─ components/
│  ├─ ui/          shadcn komponentləri (button, card, dialog, sheet, table, badge, …)
│  └─ common/      PageHeader, EmptyState, ConfirmDialog, Pagination, StatusBadge, StatCard, FileDropzone
├─ features/
│  ├─ auth/        LoginPage, RegisterPage, useCurrentUser
│  ├─ dashboard/   DashboardPage
│  ├─ documents/   UploadPage, DocumentList, useDocument (polling)
│  ├─ questions/   QuestionBankPage, QuestionFilters, QuestionEditSheet
│  └─ exams/       ExamSetupPage, ExamSessionPage, ExamResultPage, ExamHistoryPage,
│                  QuestionCard, OptionList, QuestionNavigator, ExamTimer
├─ lib/            api-client.ts, api-types.ts (generated), query-keys.ts, format.ts, utils.ts
└─ index.css       Tailwind + design tokens
```

Qayda: hər feature öz `api.ts` (fetch funksiyaları + query hook-ları) faylına malikdir. Komponent yalnız 2+ feature istifadə edəndə `components/common`-a köçürülür.

## 3. Route-lar

| Route | Səhifə | Layout |
|---|---|---|
| `/login`, `/register` | Auth | Mərkəzləşdirilmiş kart |
| `/` | Dashboard | AppLayout |
| `/upload` | PDF yükləmə + yüklədiklərim | AppLayout |
| `/questions?documentId=&status=&search=&page=` | Sual bankı (filtrlər URL-də) | AppLayout |
| `/exams/new` | İmtahan setup | AppLayout |
| `/exams` | Tarixçə | AppLayout |
| `/exams/:id?q=` | İmtahan sessiyası | **ExamLayout** (sidebar yox, diqqət rejimi) |
| `/exams/:id/result` | Nəticə | AppLayout |

`RequireAuth` token yoxdursa və ya `/api/auth/me` 401 qaytarırsa giriş ekranını göstərir (Daxil ol / Qeydiyyat / Şifrəmi unutdum). `/reset-password?uid=&token=` — açıq route (emaildəki link). JWT `localStorage`-da; istənilən 401 tokeni silir.

## 4. Səhifələr

**Dashboard** — 4 StatCard (bankdakı suallar, yüklədiklərim, imtahanlarım, orta nəticə); aktiv imtahan varsa üstdə banner "Davam et (qalan vaxt)"; son 5 imtahan; tez əməliyyatlar: "PDF yüklə", "İmtahana başla".

**Upload** — FileDropzone (drag & drop + seç düyməsi, client tərəfdə tip/ölçü yoxlaması) → progress bar → emal statusu (2 s-də bir polling, terminal statusda dayanır) → report kartı (tapılan / avtomatik / Unverified / dublikat / xəbərdarlıqlar) → "Suallara bax". Altında yüklədiyim sənədlərin siyahısı (status, sual sayı, sil, yenidən emal et).

**Question Bank** — filtr paneli: sənəd (select), status (Hamısı / Unverified / Avtomatik / Təsdiqlənmiş), "Yalnız xəbərdarlıqlılar", axtarış (300 ms debounce). Cədvəl: №, mətn (2 sətir), sənəd, status badge, əməliyyatlar. Redaktə sağdan açılan **Sheet**-də: sual mətni, variantlar (əlavə / sil / yuxarı-aşağı), düzgün cavab radio-su, "Saxla". AutoDetected sual üçün bir kliklə "Təsdiqlə". `canEdit = false` olanda redaktə gizlədilir. Mobil: cədvəl kart siyahısına çevrilir.

**Exam Setup** — PDF (məcburi), sual aralığı (başlanğıc/son nömrə, default PDF-in tam aralığı; `from ≤ to` və PDF nömrələri daxilində yoxlanılır), sual sayı (10/20/30/50 + istənilən rəqəm), cavab rejimi (sonda / hər sualdan sonra). Canlı xülasə (`/api/exams/availability`): mövcud unikal sual, vaxt (1 sual = 1 dəq), ballanmayacaq suallar. Kifayət qədər sual yoxdursa "Başla" deaktivdir və səbəb göstərilir.

**Exam Session** (ExamLayout):
```
┌──────────────────────────────────────────────────────────┐
│ ExamFlow   Sual 12 / 30  ▓▓▓▓░░░░ 40%   ⏱ 31:20 [Bitir]│
├───────────────────────────────────────┬──────────────────┤
│  Sual 12                              │ Naviqasiya       │
│  ASP.NET Core nədir?                  │ [1][2][3][4][5]  │
│  ○ A  Database sistemi                │ [6][7]…          │
│  ● B  Web framework                   │ ■ cavablı □ boş  │
│  ○ C  Operating system                │                  │
│  [← Əvvəlki]           [Növbəti →]    │ 12/30 cavablanıb │
└───────────────────────────────────────┴──────────────────┘
```
- Mobil: naviqasiya paneli düymə ilə açılan Sheet-ə keçir; Prev/Next aşağıda sabit bar-da.
- Cavab seçimi optimistic update → arxa planda PUT; "Saxlanıldı ✓" / xətada "Yenidən cəhd" indikatoru; saxlanmamış cavab varsa `beforeunload` xəbərdarlığı.
- Taymer: son 5 dəqiqədə amber, son 1 dəqiqədə qırmızı; `aria-live="polite"` yalnız 5 və 1 dəqiqə qalanda elan edir.
- "Bitir" → ConfirmDialog: "3 sual cavabsızdır. Bitirmək istəyirsiniz?"

**Exam Result** — böyük nəticə dairəsi (faiz) + saylar (düzgün / yanlış / cavabsız / qiymətləndirilməyib), müddət. Tab-lar: Hamısı / Səhvlər / Cavabsız / Qiymətləndirilməyən. Hər sual kartında seçilmiş və düzgün variant ikon + rənglə işarələnir. "Yenidən imtahan ver" və "Dashboard".

## 5. Dizayn sistemi

| Token | Dəyər |
|---|---|
| Background | `slate-50` (#F8FAFC) |
| Surface | white, border `slate-200`, kölgə `shadow-sm` |
| Text | primary `slate-900`, secondary `slate-600`, muted `slate-500` |
| Primary | `indigo-600` (hover `indigo-700`) |
| Status | success `emerald-600`, danger `rose-600`, warning `amber-500`, info `sky-600` |
| Radius | 8px (input, button), 12px (card) |
| Spacing | 4px şkala; səhifə padding 24px (desktop) / 16px (mobil); kartlar arası 16–24px |
| Tipoqrafiya | Inter; body 14px/20px; səhifə başlığı 24px/600; imtahan sual mətni 18px/28px |
| Max eni | content 1200px; imtahan sütunu 768px |
| Breakpoint-lər | Tailwind default (`sm 640`, `md 768`, `lg 1024`) |
| Animasiya | Yalnız 150 ms transition (hover, sheet/dialog açılışı); `prefers-reduced-motion` nəzərə alınır |

Status badge rəngləri bütün səhifələrdə eynidir: Unverified = amber, Avtomatik = sky, Təsdiqlənmiş = emerald, Failed = rose.

## 6. Accessibility

- Variantlar Radix `RadioGroup` (ox düymələri, düzgün rollar).
- Klaviatura: `A–F` və ya `1–6` variant seçir, `←/→` əvvəlki/növbəti sual; qısayollar input fokusda olanda işləmir.
- Görünən focus ring (`ring-2 ring-indigo-500`), bütün interaktiv elementlər klaviatura ilə əlçatan.
- Rəng yeganə göstərici deyil: düzgün/yanlış üçün ikon (✓/✕) və mətn.
- Kontrast WCAG AA (mətn ≥ 4.5:1).
- Hər səhifədə tək `h1`, formalarda `label` + xəta mesajı `aria-describedby` ilə.

## 7. Data axını qaydaları

- Query key-lər `lib/query-keys.ts`-də mərkəzləşdirilib.
- Mutasiyadan sonra yalnız əlaqəli key-lər invalidate olunur (məs. sual redaktəsi → `questions.list`, `questions.detail(id)`).
- İmtahan sessiyası bir dəfə yüklənir; cavablar cache-də optimistic yenilənir, sessiya yenidən fetch edilmir.
- Loading → Skeleton, boş → EmptyState, xəta → inline Alert + "Yenidən cəhd".
