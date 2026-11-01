# HANDOFF — ExamR

Tarix: 2026-11-02. Repo: `C:/Users/elesg/OneDrive/Desktop/ExamR`.

## 1. Son sessiyadan sonra əsas dəyişikliklər

Git HEAD `9ad1592` — `Add battle mode with live chat and shared agent workflow`. Son Claude sessiyasının dəqiq sərhədi commit tarixçəsində görünmür: iş ağacında əvvəlki agentlərdən də qalan çoxlu dəyişiklik və untracked fayl var. Aşağıda bu söhbətdə tamamlanan işlər və qorunan kontekst verilir; bütün diff-i tək agentin işi saymayın. Commit/push edilməyib, dəyişiklikləri sıfırlamayın.

Əsas işlər: AI-siz Study, auth və tətbiq dizaynı/tema, Remember Me, admin tərəfindən idarə olunan qruplar, Admin Panel, sənəd paylaşımı və onların lokal bazaya/API-yə tətbiqi.

## 2. Əlavə və dəyişdirilmiş funksionallıq

- **Study:** OpenAI çağırışları çıxarılıb. Mövcud PDF/DOCX sual-cavabları istifadə olunur. “Məni yoxla”: manual say, yazılı cavabın saxlanması, dərhal təxmini söz uyğunluğu faizi/çatışmayan anlayışlar, ipucu, ilk cümlələrlə izah, tam cavab və növbəti sual.
- **Variantlı Study:** çətinlik seçimi yoxdur; cavablardan dörd variant, nəticə/səhvlər/zəif mövzular, tarixçə/davam/silmə. Yeni sual maksimum 240, variant maksimum 220 simvoldur. Yetərli uyğun sual yoxdursa seçilmiş sayı səssiz azaltmaq əvəzinə mövcud sayı bildirən 400 qaytarılır. Ortalıq sözlərə görə həddindən artıq sərt filtr yumşaldılıb.
- **Auth/dizayn:** iki sütunlu Login/Register/Forgot/Reset, dekorativ kartlar, şifrə göstər/gizlət, yumşaq tab/panel keçidi, yığcam checkbox. İstənməyən şüarlar silinib. Light/Dark tema bütün tətbiqə yayılıb; sidebar, kartlar, input/düymələr uyğunlaşdırılıb. Tema açarı `examr-auth-theme`, sistem və digər tab dəyişiklikləri izlənir.
- **Remember Me:** login seçilməzsə standart 60 dəqiqə JWT/sessionStorage; seçilərsə konfiqurasiya olunan 7 gün/localStorage. Şifrə saxlanmır; logout/reset SecurityStamp ilə tokenləri ləğv edir. Refresh token əlavə edilməyib.
- **Qruplar:** bütün Admin-lər CRUD və nickname axtarışı ilə çoxlu üzv əlavə/çıxarma edə bilər. Tələbə yalnız üzv olduğu qrupları və üzvlərin avatar/nickname-lərini görür. Əlavə olunma mövcud qalıcı bildiriş/SignalR vasitəsilə çatdırılır. Təkrar ad/üzvlük bloklanır.
- **Admin Panel:** `/admin`, mövcud login və Admin rolundan istifadə edir; sənəd upload/parser və mövcud qrup səhifəsi inteqrasiya olunub. Ayrıca admin login yaradılmayıb.
- **Paylaşım:** yalnız sahib, birbaşa istifadəçilər, qruplar və ya bütün autentifikasiya olunmuş istifadəçilər. Birbaşa və qrup paylaşımı birlikdə mümkündür. “Mənim sənədlərim” və “Mənimlə paylaşılanlar” ayrıdır; qorunan download əlavə edilib.

## 3. Əsas fayllar və məqsədləri

Yollar repo kökünə nəzərən verilir; `src/ExamFlow.Api/` backend, `client/src/` frontend köküdür.

| Fayl/qovluq | Məqsəd |
|---|---|
| Backend `Features/Study/*` | Lokal hazırlama/qiymətləndirmə, sessiya action/history/delete; `StudyAiClient.cs` artıq boş uyğunluq sinfidir. |
| Frontend `features/study/{StudyPage.tsx,api.ts}` | Say seçimi, yazılı/variantlı məşq və nəticə UI-si. |
| `Features/Auth/ConfiguredAdmins.cs` | Server allowlist, etibarlı mövcud hesab ID-ləri və email təsdiqli admin təyinatı. |
| `Features/Auth/{AuthEndpoints,IdentitySeeder}.cs`, `Infrastructure/Security/TokenService.cs` | Login/register JWT, admin təsdiqi/rolu, Remember Me müddəti. |
| `features/auth/{AuthPage,ResetPasswordPage}.tsx`, `auth.css` | Auth dizaynı və stabil keçidlər. |
| `lib/{auth-token,use-theme}.ts`, `components/common/ThemeToggle.tsx` | Token saxlanması və vahid qalıcı tema. |
| Backend `Features/Groups/*`; frontend `features/groups/*` | Qrup entity/config/DTO/endpoint və tələbə/admin/modallar. |
| `features/admin/AdminPage.tsx` | Admin bölmələri; əvvəlki qrup funksiyasını yenidən yaratmır. |
| `Features/Documents/{DocumentAccess,DocumentShare,DocumentSharingEndpoints}.cs` | Dinamik oxuma/Battle icazəsi, grant modelləri və owner-only admin paylaşım API-si. |
| `Features/Documents/{UploadedDocument,DocumentEndpoints}.cs` | Public flag, siyahı/read/download və paylaşım statusu; dəyişdirmə/silmə sahibə məhduddur. |
| `Features/{Questions/QuestionEndpoints,Study/StudyEndpoints,Exams/ExamEndpoints}.cs` | Paylaşılan mənbələr və ləğv olunmuş girişin yoxlanması. |
| `Features/Battles/{BattleEndpoints,BattleService}.cs`, `features/battle/BattleHomePage.tsx` | Admin/paylaşılan sənədləri Battle-dan çıxarır; yaradılma və başlanma da yoxlanır. |
| `Features/Friends/SocialHub.cs`, frontend bildiriş faylları | Qrupa əlavə olunma bildirişi; dostluq məntiqi dəyişdirilməyib. |
| `features/documents/{UploadPage,SharedDocuments,DocumentSharingModal}.tsx`, `api.ts`, `sharing-api.ts` | Upload-u təkrar istifadə, şəxsi/paylaşılan siyahılar və paylaşım modalı. |
| `app/{AppLayout,providers,router}.tsx`, `index.css`, ortaq `Button/FormField` | Sidebar/route/rol görünməsi, tema və ümumi dizayn. |
| `lib/{api-client,query-keys}.ts` | JWT ilə fayl download-u, yeni query açarları. |
| `Infrastructure/Data/AppDbContext.cs`, `Program.cs`, migrations/snapshot | Modellərin, endpoint-lərin və DB dəyişikliklərinin qoşulması. |

Əvvəldən qalan DOCX parser, nickname/avatar, dostluq, qalıcı Battle otaqları və ownership dəyişiklikləri də uncommitted-dir. Bunları overwrite etməyin; son işdə parser yenidən yazılmayıb.

## 4. Arxitektura və database

Feature-folder Minimal API + birbaşa DbContext arxitekturası saxlanıb. Yeni dependency yoxdur. Qrup/paylaşım yazmaları EF execution strategy daxilində serializable tranzaksiyalardadır.

Bu sessiyada yaradılmış və **sonradan tətbiq edilmiş** migration-lar:

- `20261102030000_AddStudyGroups`: `StudyGroups` (ad, normalizə edilmiş unikal ad, təsvir, tarix), `StudyGroupMembers` (GroupId/UserId composite PK). Qrup silinməsi üzvlükləri cascade edir, istifadəçilər silinmir.
- `20261102040000_AddDocumentSharing`: `UploadedDocuments.ShareWithEveryone` (mövcud sənədlər üçün false), `DocumentUserShares`, `DocumentGroupShares` composite PK/FK-ləri. Designer və snapshot mövcuddur.

Əvvəlki uncommitted migration-lar: FriendsAndNotifications, PersistentBattleRooms, UserProfiles, SmartStudy, StudyAi, ScopeDocumentDuplicatesToOwner. TASKS onları əvvəl tətbiq edilmiş kimi qeyd edir; son database update yalnız yuxarıdakı iki migration-ı tətbiq etdi. Study AI adlanan köhnə cədvəllər saxlanılıb: sessiya JSON-u `StudyAiTurns` (`Status=Local`), cavab/nəticələr `StudyAttempts`-dədir.

## 5. API və biznes qaydaları

Bütün yollar `/api` prefikslidir:

- Study: `GET /study/documents`, `GET /study/{documentId}/progress`; `POST /study/practice`, `GET/DELETE /study/practice/{id}`, `POST /study/practice/{id}/action`, `GET /study/{documentId}/practice/history`. Action: next/hint/explain/reveal/answer/respond. Sahiblik, mənbəyə cari giriş və aktiv imtahan məhdudiyyəti qorunur.
- Qrup: `GET /groups`, `/groups/{id}`, `/groups/{id}/members`.
- Admin qrup: `GET/POST /admin/groups`; `PUT/DELETE /admin/groups/{id}`; `GET /admin/groups/{id}/users?q=...`; `POST /admin/groups/{id}/members`; `DELETE /admin/groups/{id}/members/{userId}`.
- Paylaşım: `GET /admin/documents/users?q=...`; `GET/PUT /admin/documents/{id}/sharing`. Admin rolu **və** sənəd sahibliyi lazımdır.
- Sənəd: `GET /documents/{id}/download`; list üçün `shared=true`, `includeShared=true`, `forBattle=true`. Standart/mine şəxsi sənədlərdir.
- `GET /auth/confirm-admin?userId=...&token=...`: yalnız konfiqurasiya edilmiş ünvan üçün etibarlı Identity email tokeni ilə təsdiq.

Qrup üzvlüyü dinamik yoxlanır: çıxarılan üzv həmin qrupun grantını itirir, yeni üzv avtomatik əldə edir. Alternativ direct/public hüququ varsa giriş davam edir. Paylaşılan sənəd redaktə/silmə/reprocess hüququ vermir. İcazəsiz resurs 404, autentifikasiya olunmuş qeyri-admin üçün admin API 403 qaytarır. Admin başqasının private sənədini sırf roluna görə görə bilmir. Admin/paylaşılan sənədlər Battle üçün bloklanır. Şəxsi sənədlərin görünməsi dəyişməyib.

## 6. Məlum problemlər və yoxlanılmayan hissələr

- Admin hesabı ilə müsbət qrup CRUD/üzv/bildiriş və real public/direct/group paylaşım, ləğv, Study/exam/Battle kombinasiyaları **canlı yoxlanılmayıb**; tam UI və Dark/Light vizual QA da son mərhələdə aparılmayıb.
- Əl ilə JWT imzalama diaqnostikasının ilk admin sorğusu 401 aldı; davamı auto-review tərəfindən normal girişdən kənar credential istifadəsi kimi rədd edildi. Bu üsulu təkrarlamayın; normal admin girişindən istifadə edin.
- Study qiyməti semantik deyil, söz uyğunluğudur; sinonim/inkarı tam tanımır. Distraktorların semantik etibarlılığı zəmanətli deyil. Qısa etibarlı variantlar az olduqda say seçimi 400 ala bilər.
- Köhnə məşqlərin sayı/uzun variantları dəyişdirilməyib. Köhnə sessiyada əlaqəsi saxlanmayan StudyAttempt-lər sessiya silinərkən qala bilər.
- Refresh token yoxdur; standart 60 dəqiqə uzun imtahan zamanı bitə bilər. Vite >500 kB chunk xəbərdarlığı qalır. Dashboard real statistika və sual bankı redaktəsi hələ hazır deyil.
- Admin verification email axını canlı yoxlanılmayıb; development email-ləri SMTP əvəzinə `App_Data/mail` pickup-da qala bilər.

## 7. Növbəti konkret tapşırıq

`elesgerovrrevan@gmail.com` mövcud hesabına **faktiki Admin rolu verilib**: yalnız həmin hesab üçün idempotent SQL membership yenilənməsi edilib, yeni hesab yaradılmayıb. Server user-secrets-da `AdminAccess:Emails:*` və operator tərəfindən etibar edilən `AdminAccess:ExistingUserIds:*` saxlanıb; dəyərləri çap/commit etməyin. Yeni hesablar üçün yalnız email yazmaq kifayət etmir: allowlist + email təsdiqi lazımdır; mövcud etibarlı ID alternatividir. Çox admin dəstəklənir.

İstifadəçi hesabdan çıxıb normal login ilə yeni JWT alsın, `/admin` açılsın. Sonra kiçik müvəqqəti qrup/sənədlə: CRUD, duplicate ad/üzvlük, nickname seçimi və bildiriş; direct+group/public paylaşımı; üzv əlavə/çıxarma və grant ləğvi; download/Study/exam girişləri və Battle blokunu yoxlayın. Yalnız yaradılan sınaq məlumatlarını təmizləyin; mövcud sənədləri paylaşmayın və hesab şifrələrini dəyişməyin. Tapılan konkret xətanı minimum dəyişikliklə düzəldin. Commit üçün ayrıca istifadəçi təsdiqi lazımdır.

## 8. Son build/test/tətbiq vəziyyəti

Son yoxlamalar istifadəçinin ayrıca icazəsi ilə aparılıb; bu handoff yaradılarkən yenidən işlədilməyib:

- `dotnet build --no-restore`: **0 xəta / 0 xəbərdarlıq**. Aşkarlanan `DocumentEndpoints.Project` çatışmayan DbContext parametri düzəldilib.
- `dotnet test --no-build --no-restore`: **57/57** (API 25, parser 32).
- `npm run build` və `npx oxlint`: uğurlu; yalnız Vite chunk ölçüsü xəbərdarlığı.
- `dotnet ef migrations has-pending-model-changes --project src/ExamFlow.Api --no-build`: model/snapshot uyğundur.
- Database update uğurlu; yuxarıdakı iki migration tətbiq edildi. API restart edildi; son yoxlamada API `localhost:5180`, UI/proxy `localhost:5190` işləyirdi. Hazırkı proseslər bu handoff zamanı yenidən yoxlanılmayıb.
- Normal qeydiyyat/login/Remember Me müddəti, qrup siyahısı **200**, tələbə admin API **403**, private resurs/download/delete **404**, owner download **200**, DOCX upload/parser, Study mənbəyi və şəxsi/paylaşılan siyahı ayrılığı canlı keçdi. Müvəqqəti iki hesab/sənəd təmizləndi. **Qrup route 404 problemi artıq aradan qaldırılıb**; TASKS-də köhnə “migration tətbiq edilməyib/404 qalır” sətirləri tarixi qeyddir.
