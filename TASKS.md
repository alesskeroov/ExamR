# TASKS.md — ExamFlow

Agentlər (Claude Code, OpenAI Codex) arasında ötürmə faylı. Qaydalar: `AGENTS.md` §6.
Hər tapşırıq bitəndə yenilənir. Tarixlər: YYYY-MM-DD.

## Davam edən iş / Handoff

_Hazırda yarımçıq implementasiya və tətbiq işi yoxdur. Admin hesabı ilə müsbət CRUD/paylaşım axınının canlı yoxlanması üçün istifadəçinin normal giriş sessiyası lazımdır._

<!--
Yarımçıq iş qalanda bu bölməni doldurun (nümunə):

**Tapşırıq:** <istifadəçinin istəyi 1–2 cümlə>
**Agent / tarix:** Claude Code | Codex — 2026-11-02
**Görülüb:** ...
**Qalıb (ardıcıllıqla):**
1. ...
**Dəyişdirilmiş / yeni fayllar:** (`git status --short`)
**Build / testlər:** dotnet build ✅ | dotnet test ✅ 45/45 | npm run build ✅ — və ya nəyin niyə sınığı
**Migration:** yoxdur | `<Name>` yaradılıb, tətbiq olunub / olunmayıb
**Diqqət:** növbəti agent nəyi etməməli / istifadəçidən nəyi soruşmalıdır
**Təklif olunan commit:** fayllar + mesaj (istifadəçi təsdiq etmədən commit/push yoxdur)
-->

## Tamamlanmış işlər

- Admin Panel: rollar hər sorğuda DB-dən oxunur (qrup yaratma 403 düzəldi, rol geri alınanda köhnə JWT admin girişi itirir); "Adminlər" bölməsi + `AdminRoleAudits` audit; paylaşım UI yalnız Qruplar/Hamı. Migration `20261102050000_AddAdminRoleAudits` **tətbiq edilməyib**, build/test işlədilməyib (2026-11-02).

| Tarix | İş | Commit |
|---|---|---|
| 2026-11-02 | İstifadəçi icazəsi ilə Admin/Groups/Sharing tətbiq edildi: DocumentEndpoints.Project-ə çatışmayan DbContext parametri əlavə olundu; solution build 0 xəta/0 xəbərdarlıq, .NET testləri 57/57, frontend build və oxlint uğurlu (Vite bundle >500 kB xəbərdarlığı). EF model/snapshot uyğunluğu təsdiqləndi; AddStudyGroups və AddDocumentSharing migration-ları MSSQL-də tətbiq olundu; API restart edildi. Canlı normal register/login/remember expiry, groups 200, tələbə admin API 403, private document IDOR/download/delete 404, DOCX parsing/Study və şəxsi/paylaşılan siyahıları yoxlanıldı. Müvəqqəti 2 hesab və sənəd silindi. Admin pozitiv CRUD/paylaşım axını normal admin sessiyası olmadan yoxlanmadı; əl ilə JWT signing-key token diaqnostikasının davamı auto-review tərəfindən rədd edildi və davam etdirilmədi. Mövcud hədəf hesabın Admin rolu və user-secrets konfiqurasiyası əvvəlki tapşırıqda verilib; yeni JWT üçün logout/login lazımdır. | Commit edilməyib |
| 2026-11-02 | Admin Panel və private/user/group/public sənəd paylaşımı implementasiya edildi; mövcud hədəf hesaba Admin rolu faktiki verildi, server user-secrets allowlist/trusted ID yazıldı. AddDocumentSharing migration yaradıldı, tətbiq edilmədi; build/test/restart qadağası səbəbilə runtime qrup 404-ü qalır (handoff-a bax). | Commit edilməyib |
| 2026-11-02 | Study Groups implementasiyası: admin CRUD və çoxlu üzv idarəetməsi, nickname axtarışı, tələbə qrupları/üzvlər, qalıcı və canlı bildiriş; AddStudyGroups migration/designer/snapshot yaradıldı. Build/test/migration tətbiqi istifadəçi qadağası səbəbilə edilmədi; tətbiq addımları handoff-da. | Commit edilməyib |
| 2026-11-02 | Girişdən sonrakı UI auth dizaynına uyğunlaşdırıldı: vahid qalıcı light/dark tema (əvvəlki açar qorunur, sistem/başqa tab dəyişiklikləri izlənir), desktop/mobile tema düyməsi, yenilənmiş sidebar/yuxarı panel, ortaq kart/input/düymə üslubu, status və modal rəngləri. Funksional API/backend dəyişmədi. İstifadəçi tələbi ilə test/build işlədilmədi; canlı vizual yoxlama edilmədi. | Commit edilməyib |
| 2026-11-02 | Auth UI: yuxarı və sol alt şüarlar silindi; input/düymə arasında 116px məcburi boşluq və boş xəta sahəsi çıxarıldı, keçid paneli yumşaq grid hündürlük animasiyasına keçirildi. Məni xatırla tema uyğun kompakt seçim, seçili/fokus state-ləri ilə yeniləndi. Yalnız AuthPage.tsx/auth.css/TASKS.md; auth məntiqi dəyişmədi. İstifadəçi tələbi ilə test/build işlədilmədi. | Commit edilməyib |
| 2026-11-02 | Auth: yuxarı mətn badge oldu, formadakı əlavə mətn silindi, sabit grid-də fade/slide və hərəkətli tab göstəricisi əlavə edildi. Login RememberMe serverə ötürülür: seçiləndə konfiqurasiya olunan 7 günlük JWT + localStorage, seçilməyəndə standart 60 dəqiqə + sessionStorage; şifrə saxlanmır, SecurityStamp ləğvi qorunur. İstifadəçi qadağası ilə test/build/restart işlədilmədi; RememberMe müddətinin tətbiqi üçün backend ayrıca build/restart edilməlidir. Migration/dependency yoxdur. | Commit edilməyib |
| 2026-11-02 | Auth frontend dizaynı: iki sütunlu ExamR giriş/qeydiyyat/bərpa ekranı, dekorativ imtahan kartları, auth-a məhdud qalıcı light/dark tema, şifrə göstər/gizlət, sabit keçid sahəsi və reduced-motion dəstəyi. AuthPage.tsx, ResetPasswordPage.tsx, auth.css dəyişdi; API/backend/dependency dəyişmədi. İstifadəçi göstərişi ilə test/build işlədilmədi; canlı vizual yoxlama edilmədi. | Commit edilməyib |
| 2026-11-02 | Study sual sayı: variant filtrində ümumi sənəd sözlərinin bütün alternativləri rədd etməsi yumşaldıldı, yalnız ilk cümlə əvəzinə ilk uyğun qısa tam cümlə seçilir; eyni/oxşar cavab yoxlaması qorunur. Seçilən sayda uyğun sual yoxdursa səssiz 1 suallıq məşq əvəzinə mövcud sayı bildirən 400 qaytarılır. Məni yoxla və variantlı rejim üçün ortaq manual say/başlatma əlavə edildi. Backend build 0 xəta/0 xəbərdarlıq, restart; proxy API 401, Vite modul 200. Test/frontend build və girişli real sənəd axını yoxlanılmadı; migration yoxdur. | Commit edilməyib |
| 2026-11-02 | Study: uzun Prompt/ExpectedAnswer-in 4000 limitli sahəyə yazılması düzəldildi (tam mənbə cavabı qorunur); açıq cavab üçün dərhal AI-siz anlayış uyğunluğu faizi/çatışmayan sözlər və təkrar vaxtı saxlanır. Variantlı rejimdə çətinlik seçimi/filtri çıxarıldı; yeni suallar 240, tam ilk cümlədən götürülən variantlar 220 simvolla məhduddur. Təsdiqli məşq silmə əlavə edildi (yalnız sahibin sessiyası və məlum bağlı cəhdləri; köhnə sessiyalarda əlaqəsi saxlanmayan cəhdlər qorunur). Backend build 0 xəta/0 xəbərdarlıq və restart; Study POST/DELETE proxy-dən 401, Vite Study səhifəsi 200. Test/frontend build və girişli tam axın yoxlanılmadı; migration/dependency yoxdur. | Commit edilməyib |
| 2026-11-02 | Study Məni yoxla rejiminə yazılı cavab sahəsi/göndərmə əlavə edildi; mətn mövcud StudyAttempt və sessiya JSON-unda saxlanır, növbəti sualda sıfırlanır. AI/avtomatik bal/migration yoxdur. Backend build 0 xəta/0 xəbərdarlıq və restart; proxy Study 401. Test/frontend build və girişli UI axını yoxlanılmadı. | Commit edilməyib |
| 2026-11-02 | Study 404 istifadəçi təsdiqi ilə backend build və restart edilərək aradan qaldırıldı: 0 xəta/0 xəbərdarlıq. Canlı POST practice, GET history və Vite proxy practice autentifikasiyasız 401 verir (əvvəl 404). Testlər/frontend build işlədilmədi; yeni kod dəyişikliyi və migration yoxdur. | Commit edilməyib |
| 2026-11-02 | Study AI çağırışları silindi; lokal anlayış ipucu/ilk cümlələr/tam cavab, mətn oxşarlığı ilə konservativ 4 variant seçimi və təxmini çətinlik əlavə edildi. Cavabsız suallar çıxarılır; nəticə/tarixçə mexanizmi saxlanıldı. Sənəddə açıq sual olsa da onu rədd edən çoxluq filtri çıxarıldı. Test/build/canlı yoxlama istifadəçi göstərişi ilə işlədilmədi; migration yoxdur. Əvvəlki handoff-dakı route 404 üçün cari backend-in build/restart edilməsi hələ lazımdır; bu sessiyada 5180 listener görünmədi. | Commit edilməyib |
| 2026-11-02 | Yalnız Study yenidən quruldu: sərbəst chat/grade route-ları çıxarıldı; sənəddən təsadüfi açıq sual + yalnız ipucu/sadə izah/cavab və növbəti sual; çətinlikli A/B/C/D sessiyaları, serverdə AI-siz qiymətləndirmə, nəticə/yanlış izahları/zəif mövzular, davam/keçmiş. AI yalnız ipucu/izah/variant yaradır; istifadəçi/sənəd/sual/mətn hash-i/model/çətinlik üzrə qalıcı cache. Sessiya JSON-u StudyAiTurns (Local), cavablar StudyAttempts ilə saxlanır; sahiblik, aktiv imtahan qoruması, paralel dəyişiklik yoxlaması saxlanıldı. Migration/dependency yoxdur. İstifadəçi tələbi ilə test/build/canlı yoxlama işlədilmədi; yeni backend kodu işləyən API-yə tətbiq edilməyib. | Commit edilməyib |
| 2026-11-02 | Görünməyən UNEC PDF SHA-256 ilə eyniləşdirilib köhnə test hesabından istifadəçinin göstərdiyi hesaba bərpa edildi (669 sual və ID-lər qorundu). Dublikat yoxlaması və unikal indeks istifadəçi üzrə edildi; ScopeDocumentDuplicatesToOwner migration tətbiq olundu. Bütün 5 window.confirm saytdaxili əlçatan vahid modal ilə əvəzləndi (sənəd silmə, otaq bağlama, Host ötürmə, iştirakçı çıxarma). İstifadəçi təsdiqi ilə backend build 0 xəta/0 xəbərdarlıq, API restart; test/frontend build işlədilmədi. | Commit edilməyib |
| 2026-11-02 | Study sənəd seçimi İmtahan üslubunda daimi kart/dropdown və sual sayı ilə göstərilir. Upload-da ayrıca tam enli şəxsi sənədlər bölməsi, say/səhifələmə, yeni upload keçidi və təsdiqli silmə əlavə edildi; silinən seçim/keş təmizlənir, İmtahan/Study mənbələri yenilənir. Backend/migration dəyişmədi; istifadəçi göstərişinə əsasən test/build işlədilmədi, canlı UI yoxlanılmadı. | Commit edilməyib |
| 2026-11-02 | Upload şəxsi sənədlər siyahısına səhifələmə və xəta təkrarı əlavə edildi. Study açılış 404 səbəbi işləyən köhnə API idi; istifadəçi təsdiqi ilə backend build (0 xəta/0 xəbərdarlıq) və restart edildi, route JWT-siz 401 verir. DOCX cavab daxilindəki siyahıların variant sayılması düzəldildi; açıq cavab strukturu prioritetləşdirildi, köhnə qeydlər ID/tarixçə/variantlar silinmədən zənginləşdirildi (35 sual, 18→25 açıq cavab). Test/frontend build işlədilmədi, migration yoxdur. | Commit edilməyib |
| 2026-11-02 | Sənəd/sual/imtahan/Study/Battle yaradılması JWT sahibinə məhdudlaşdırıldı (başqası üçün 404); Study açıq cavab strukturu, imtahan variant strukturu ilə süzülür. Başlanğıcda köhnə DOCX cavabları sual ID-ləri və tarixçə silinmədən bərpa edilir; duplicate cavabı başqa sahibin ID-sini açıqlamır, girişdə əvvəlki sorğu keşi təmizlənir. Test/build istifadəçi tələbi ilə işlədilmədi; migration yoxdur. Dəyişikliklərin və bərpanın işləməsi üçün cari kodla API restart lazımdır. | Commit edilməyib |
| 2026-11-02 | Study sənəd siyahısı yalnız izahlı açıq sualı olan sənədlərlə məhdudlaşdırıldı; mənbə və irəliləyiş filtrləri də həmin suallardan istifadə edir. Chat-a “Məni yoxla” və “Variantlı sual” rejimləri, rejimə uyğun köməkçi düymələr və cavab göstərmək üçün yeni əmr əlavə edildi. Build/test istifadəçi istəyinə əsasən işlədilmədi; migration yoxdur. | Commit edilməyib |
| 2026-11-02 | Smart Study `/study/message` 500 xətası: SQL Server `Explanation` və `AnswerOptions.Text` sütunlarının fərqli collation-ları onları LINQ `CASE` proyeksiyasında birləşdirəndə toqquşurdu. Mənbələr izahlı cavablar və düzgün variantlar üzrə iki EF sorğusuna ayrıldı. API build 0 xəta/xəbərdarlıq, testlər işlədilmədi; API 5180-də yenidən başladı. | Commit edilməyib |
| 2026-11-02 | Study OpenAI Responses API-yə keçirildi: konfiqurasiya olunan `gpt-4.1-mini`, PDF/DOCX-dən leksik RAG (məhdud əlaqəli hissələr), Azərbaycan dilində kontekstli chat/ipucu, açıq və A/B/C/D məşqləri, mənaya görə qiymətləndirmə, mənbə istinadları, qalıcı tarixçə/aktiv məşq və mövcud təkrar sistemi. MSSQL-də istifadəçi üzrə atomik gündəlik 50 AI sorğusu rezervasiyası, token/cached-token/təxmini USD uçotu (admin ümumi monitorinq); timeout/429/auth/refusal/natamam cavab handling. Açar yalnız backend env və ya user-secrets; yeni dependency yoxdur. `AddStudyAi` yaradıldı və lokal DB-yə tətbiq edildi, API başladıldı. Backend build 0 xəta/xəbərdarlıq, frontend build/oxlint keçdi; istifadəçinin tələbi ilə test və real OpenAI sorğusu işlədilmədi. | Commit edilməyib |
| 2026-11-01 | DOCX upload: ayrıca ZIP/XML parser (əlavə paket yoxdur), paraqraf/avtomatik nömrələmə/bold-rəng/cədvəl mətnləri mövcud sual emalına ötürülür; uzantı/MIME/paket və ölçü yoxlamaları, 20 MB upload və 64 MB açılmış paket limiti, eyni növbə/DB/frontend; PDF parseri dəyişmədi, migration yoxdur. Solution/API build 0 xəta/xəbərdarlıq; frontend build/oxlint keçdi (mövcud bundle xəbərdarlığı). API yenidən başladıldı; istifadəçinin tələbi ilə test və canlı funksional yoxlama aparılmadı. | Commit edilməyib |
| 2026-11-01 | Görünən sayt adı ExamR edildi: tab başlığı, giriş/naviqasiya, ana səhifə aria-label-ləri və şifrə bərpası email brendi. Daxili namespace/JWT/localStorage identifikatorları saxlanıldı. | Commit edilməyib |
| 2026-11-01 | Smart Study Assistant (`/study`): mövcud parserdə nömrəli açıq suallar üçün `Cavab:`/`İzah:` (ayrı sətirdə və inline), PDF-dən leksik axtarış və mənbə nömrəsi, Azərbaycan dilində chat/əmrlər, təxmini anlayış/oxşarlıq qiymətləndirməsi, mənbə əsaslı boşluq variantları, istifadəçi üzrə MSSQL nəticələr/zəif suallar və 10 dəq–30 gün təkrar planı. `AddSmartStudy` yaradıldı və lokal DB-yə tətbiq edildi; API yenidən başladıldı. AI/paket əlavə edilmədi. Solution build 0 xəta/xəbərdarlıq; frontend build/oxlint keçdi (mövcud bundle xəbərdarlığı). İstifadəçinin tələbi ilə test yazılmadı/işlədilmədi; canlı funksional yoxlama aparılmadı. | Commit edilməyib |
| 2026-11-01 | Profil şəklinin qırıq görünməsi: `<img>` JWT localStorage tokenini `Authorization` başlığına əlavə edə bilmədiyi üçün avatar GET `401` verirdi. Yalnız GET avatar endpoint-i public edildi; upload/delete auth altında qaldı. API build 0 xəta/xəbərdarlıqla keçdi, API restart edildi; auth-siz route yoxlaması 401 əvəzinə olmayan avatar üçün gözlənilən 404 verdi. | Commit edilməyib |
| 2026-11-01 | Avatar upload 404: əvvəlki API prosesi köhnə binary ilə işləyib/5180-də yox idi; cari backend build edildi, `AddUserProfiles` migration lokal MSSQL-ə tətbiq edildi, API başladıldı. Route smoke check 415 qaytardı (route mövcud, multipart body tələb olunur), 404 deyil. Sol altdakı avatar/nickname indi profil redaktə səhifəsinə keçir. Frontend build/oxlint keçdi. | Commit edilməyib |
| 2026-11-01 | Navbar xətası: köhnə API/cache user obyektində `nickname` yoxdursa `displayName` fallback istifadə olunur; auth və profil sorğuları da istifadəçi obyektini normallaşdırır. Frontend build və oxlint keçdi; test işlədilmədi. | Commit edilməyib |
| 2026-11-01 | Profil: unikal, case-insensitive nickname (`NormalizedNickname` unique index; mövcud hesablar üçün ID əsaslı backfill), nickname-only dost axtarışı, 5 MB PNG/JPEG/WebP avatar yükləmə/dəyişmə/silmə və default avatar; navbar, Friends, Battle iştirakçıları və chat-da nickname/avatar. `AddUserProfiles` migration yaradıldı. Backend build 0 xəta/xəbərdarlıq; frontend build və oxlint keçdi. | Commit edilməyib |
| 2026-11-01 | Battle çıxış qaydası yeniləndi: son online iştirakçı çıxanda otaq/üzvlüklər DB-dən silinir, gözləyən dəvətlər qeyri-aktiv edilir, nəticə tarixçəsi qalır. Hostun son tabı çıxanda qalan online iştirakçı avtomatik Host olur; çox tab və real-time state nəzərə alınır. “Çıx” dərhal, bağlantı itkisi/restart üçün 15 s reconnect müddəti; qayıdış köhnə yoxlamanı ləğv edir. Backend build 0 xəta/xəbərdarlıq, 57/57 test; iki hesabla explicit çıxış, avtomatik Host, refresh, gecikmiş disconnect və boş otaq silinməsi canlı yoxlandı. Frontend build/lint keçdi. Migration yoxdur. | Commit edilməyib |
| 2026-11-01 | Qalıcı Battle otaqları: MSSQL otaq/üzvlük və bərpa snapshot-u, “Otaqlarım” kartları (Waiting/InProgress/Completed), ad, yalnız Host bağlama; çıxış üzvlüyü/Hostu saxlayır. Host transferi kilid + rowversion ilə atomikdir, SignalR ilə yenilənir. Otaqdaxili dost modalı: online əvvəl, offline boz/disabled, ad/username axtarışı, otaqdadır/göndərildi statusları və serverdə online/limit yoxlaması. `AddPersistentBattleRooms` yaradılıb və lokal DB-yə tətbiq olunub. Backend 0 xəta/xəbərdarlıq, 54/54 test; frontend build/lint keçdi. İki hesabla çıxış/qayıdış, çoxsaylı tab, Host transferi/yeni Host Start, köhnə Hostun rəddi, dəvət/qəbul, offline rəddi və browser UI yoxlandı. API iki dəfə restart edildi: Waiting otaq və aktiv sualın cavab/deadline/chat/Host bərpası keçdi; skor/Completed/bağlama da yoxlandı. Qayda: `docs/BATTLE_ROOMS.md`. Yeni dependency yoxdur. | Commit edilməyib |
| 2026-11-01 | Dostluq sistemi + Battle dəvətləri: ad/mövcud username axtarışı, sorğu/qəbul/rədd/ləğv, dostluqdan çıxarma, blok/açma, çoxsaylı tab üçün online status; qalıcı şəxsi bildirişlər, navbar zəngi, `/hubs/social` ilə real-time yenilənmə, otaq seçim modalı, oflayn dəvət, 5 dəq expiry və qəbuldan sonra avtomatik Battle keçidi. Serializable tranzaksiyalar, unikal dostluq cütü, istifadəçi üzrə 10/dəq rate limit. `AddFriendsAndNotifications` migration yaradılıb və lokal DB-yə tətbiq edilib. Backend build 0 xəta/xəbərdarlıq, 54/54 test; frontend build/lint keçdi. İki hesabla API/SignalR və browser UI yoxlandı; ownership, self/duplicate/reverse sorğular, paralel sorğu/dəvət, offline/multi-tab presence, bloklar, expiry, başlamış/bağlanmış otaq rəddi keçdi. Lokal qayda: `docs/FRIENDS.md`. Yeni dependency yoxdur. | Commit edilməyib |
| 2026-11-01 | İmtahan və Battle sual-cavab UI: neytral answer kartları, kiçik hərf nişanları, zərif selected/reveal rəngləri, oxunaqlı typography və mobil boşluqlar; Battle-da parlaq variant fonları, həndəsi ikonlar və hərəkətli hover çıxarıldı. Yalnız `ExamSessionPage.tsx` və `BattleRoomPage.tsx` dizaynı dəyişdi. Desktop və 390 px mobil görünüşdə canlı seçim yoxlandı; Battle cavab kilidi, düzgün cavab reveal və xal göstərilməsi işləyir. Frontend build və oxlint keçdi; mövcud bundle ölçüsü xəbərdarlığı qalır. | Commit edilməyib |
| 2026-11-01 | Battle host və dəvət araşdırması: JWT/REST/host ID-ləri eyni idi; işləyən köhnə API frontend-in gözlədiyi `MyUserId`-ni göndərmirdi. API cari kodla yenidən başladıldı. Cari iştirakçı ID-si autentifikasiya olunmuş `JoinRoom` cavabından alınır; kimlik gələnədək loading göstərilir, köhnə bağlantının gecikmiş state-i nəzərə alınmır. İki lokal hesabla host Start, qonağın Start icazəsinin rəddi, real vaxt sual axını, ikitərəfli chat, iştirakçı siyahısı, reload, dəvət URL-i, girişdən əvvəl/sonra otağa qoşulma yoxlanıldı. Dəvət route/auth axını işləyir, əlavə dəyişiklik lazım olmadı. `dotnet build`: 0 xəta/xəbərdarlıq; 52/52 test; frontend build və oxlint keçdi (mövcud bundle ölçüsü xəbərdarlığı qalır). Migration yoxdur. | Commit edilməyib |
| 2026-11-01 | Planlaşdırma sənədləri (`docs/`), MSSQL-ə keçid | `084359b` |
| 2026-11-01 | Layihə strukturu: .NET 9 API, EF Core + SQL Server, React + Vite + Tailwind | `084359b` |
| 2026-11-01 | PDF parser (PdfPig): hərfli və `•` variantlar, bold/rəng/`√` cavab aşkarlanması, mərkəzləşdirilmiş cədvəl xanaları, dublikatlar; UNEC nümunəsində 669/669 | `084359b` |
| 2026-11-01 | PDF upload + arxa plan emalı, sual siyahısı (read-only), upload demo səhifəsi | `084359b` |
| 2026-11-01 | İmtahan modulu: təsadüfi seçim, taymer (1 sual = 1 dəq), cavab rejimləri, server tərəfli qiymətləndirmə, nəticə/tarixçə səhifələri | `38f7ee8` |
| 2026-11-01 | PDF üzrə sual aralığı (məs. 150–200), "bütün PDF-lər" ləğvi; Identity + JWT; şifrəmi unutdum (SMTP); tarixçədə PDF adı və aralıq | `64cd869` |
| 2026-11-01 | Agent iş axını: `AGENTS.md`, `CLAUDE.md`, `TASKS.md`; `docs/ARCHITECTURE.md` cari koda uyğunlaşdırıldı | "Add battle mode with live chat…" |
| 2026-11-01 | Battle Mode + Live Chat: SignalR hub (JWT), otaq/lobby (2–10), real-time sual axını (15–30 s, server taymeri), xal (500–1000, sürət bonusu), leaderboard, TOP 3, chat (300 simvol, 5 mesaj/10 s, mute/kick), reconnect, nəticələr tarixçədə (`AddBattleResults`) | "Add battle mode with live chat…" |

## Növbəti addımlar (prioritet sırası ilə)

1. **Token müddəti vs uzun imtahan** — JWT 60 dəq, imtahan isə 200 dəqiqəyə qədər ola bilər. 60 dəqiqədən uzun imtahanda istifadəçi ortada giriş ekranına düşür (cavablar serverdə qalır, amma taymer işləyir). Həll variantları: refresh token və ya token müddətinin uzadılması — istifadəçi ilə razılaşdırın.
2. **Phase 4 — Sual bankı:** sualı redaktə et / sil, düzgün cavabı təyin et (Unverified → Verified), axtarış və filtr; `QuestionBankPage` hazırda placeholder-dir. Bax: `docs/API_DESIGN.md` (Questions), `docs/REQUIREMENTS.md` QB-1..4.
3. **Dashboard:** real statistika (sual sayı, imtahanlar, orta nəticə, davam edən imtahan banneri).
4. **İnteqrasiya testləri:** Testcontainers.MsSql + `WebApplicationFactory` (auth, imtahan axını, icazələr).
5. **.NET 10 LTS-ə keçid** (.NET 9 dəstəyi 2026-11-də bitir): TFM + paket versiyaları.
6. **Battle təkmilləşdirmələri:** host lobby-də parametrləri dəyişə bilsin; tam chat arxivi (hazırda son 50 mesaj snapshot-da qalır); bir neçə server üçün Redis backplane və oyun koordinatoru (yalnız miqyas lazım olanda).
7. **Production deploy:** SPA-nı `wwwroot`-a köçürmək, `MapFallbackToFile`, env dəyişənləri (`docs/ARCHITECTURE.md` §4).

## Məlum problemlər / məhdudiyyətlər

- Study lokal sənəd məlumatları ilə işləyir; OpenAI açarı və token limiti tələb etmir. Variantların uyğunluğu konservativ mətn qaydaları ilə təxmin edilir; semantik ekvivalentlik tam zəmanətli deyil. Etibarlı namizəd azdırsa, sual keçilir. Açıq cavab qiyməti söz uyğunluğu əsasında təxminidir, semantik yoxlama deyil. Cari backend istifadəçi təsdiqi ilə build/restart edilib; yeni Study route-ları canlı yoxlanılıb.

- Standart token 60 dəq, RememberMe 7 gündür (konfiqurasiya olunur); refresh token yoxdur. Login tokeni seçimə görə sessionStorage/localStorage-da saxlanır. RememberMe backend kodu build/restart edilib; standart və uzun müddətli login canlı yoxlanılıb.
- Real SMTP serveri ilə test edilməyib; lokalda emaillər `src/ExamFlow.Api/App_Data/mail/*.eml`.
- Rate limit yaddaşdadır və IP üzrədir; Vite proxy arxasında bütün sorğular eyni IP sayılır.
- Parser yalnız mətn əsaslı PDF-lər üçündür (OCR yoxdur); hərfin iki dəfə çəkilməsi ilə yaradılan "fake bold" tanınmır. Real formatlardan yalnız UNEC yoxlanılıb.
- `samples/*.pdf` Git-də deyil → başqa kompüterdə `RealSampleTests` heç nə yoxlamadan keçir; Azərbaycan şrift testi Windows Arial tələb edir.
- SQL Server-də Full-Text Search quraşdırılmayıb; gələcək axtarış `LIKE` + `Azeri_Latin_100_CI_AS` ilə.
- Köhnə ("bütün PDF-lər") imtahanlarda aralıq yoxdur: tarixçədə "Bütün PDF-lər" göstərilir, "Yenidən imtahan ver" gizlədilir.
- Admin üçün UI yoxdur; ilk Admin yalnız `Seed:Admin:*` konfiqurasiyası ilə yaranır.
- Battle otaqları, üzvlük və oyun snapshot-u DB-dədir; bağlantılar/oyun dövrü bir serverdədir. Çox serverli koordinasiya yoxdur. “Otaqlarım” son 100 uyğun otağı göstərir. Keçmiş yalnız yaddaşda yaradılmış otaqlar bu migration-dan əvvəl restart-da itmişdi; yeni otaqlar qalıcıdır.
- Battle-a yalnız düzgün cavabı məlum olan suallar düşür; yarış başladıqdan sonra yeni iştirakçı qoşula bilməz (yalnız reconnect).
- Dostluq və bildirişlər DB-də qalır; dostların online statusu bir serverin yaddaşında saxlanılır. Mövcud Identity username-i email-dir; ayrıca public username redaktəsi yoxdur. Dəvət qəbul edilməsi otaqda yer rezerv etmir, `JoinRoom` aktual vəziyyəti yenidən yoxlayır. Bildiriş UI-si son 100 qeydi göstərir.
- Lokal bazada test hesabları qalıb (`*@examflow.test`) — istəsəniz silin.

<!-- Smart Study commit proposal: include Features/Study, client/features/study, parser changes, Question/DocumentProcessing, AppDbContext/Program, AddSmartStudy migration + snapshot, navigation/query keys and TASKS.md. Preserve prior unrelated changes; exclude bin/obj/node_modules/App_Data/samples/secrets. Message: Add PDF-grounded smart study assistant

- Add extractive study chat and approximate answer grading
- Persist study attempts and spaced review schedules
-->

<!-- DOCX/branding commit proposal: DocxQuestionParser.cs, DocumentEndpoints.cs, DocumentProcessing.cs, Program.cs, UploadPage.tsx, FileDropzone.tsx, AppLayout.tsx, client/index.html, AuthPage.tsx, BattleRoomPage.tsx, ExamSessionPage.tsx, AuthEndpoints.cs, EmailSender.cs, appsettings.json, TASKS.md. Shared files also contain earlier work: include only intended hunks. Exclude App_Data/bin/obj/node_modules/samples/secrets.
Add DOCX uploads and update ExamR branding

- Parse Word paragraphs, lists and tables through existing question processing
- Validate DOCX uploads and preserve PDF/database behavior
- Use ExamR for visible site and email branding
-->

<!-- OpenAI Study commit proposal: Features/Study/*, AppDbContext.cs, Program.cs, appsettings.json, AddStudyAi migration/designer/snapshot, client/features/study/*, query-keys.ts, TASKS.md. Shared files contain earlier work: stage only intended changes. Exclude secrets/App_Data/bin/obj/node_modules/samples.
Add document-grounded OpenAI study assistant

- Generate and grade study exercises with bounded document retrieval
- Persist conversations, daily quotas and token/cost accounting
- Keep API credentials on the backend and preserve existing UI
-->

<!-- Ownership/visibility commit proposal: DocumentEndpoints.cs, DocumentProcessing.cs, QuestionEndpoints.cs, ExamEndpoints.cs, BattleEndpoints.cs, StudyEndpoints.cs, client/src/features/documents/api.ts, client/src/features/auth/api.ts, TASKS.md. Include only this task’s hunks; preserve prior unrelated work. Exclude App_Data/bin/obj/node_modules/samples/secrets.
Restrict document access and restore open-answer study sources

- Enforce JWT ownership and separate study from exam document sources
- Repair legacy DOCX answers without replacing questions or history
-->

<!-- Upload/Study follow-up commit proposal: client/src/features/documents/UploadPage.tsx, client/src/features/documents/api.ts, client/src/lib/query-keys.ts, src/ExamFlow.Parsing/DocxQuestionParser.cs, src/ExamFlow.Api/Features/Documents/DocumentProcessing.cs, DocumentEndpoints.cs, Features/Study/StudyEndpoints.cs, Features/Exams/ExamEndpoints.cs, Features/Battles/BattleEndpoints.cs, TASKS.md. Stage only relevant hunks, preserve prior work; exclude App_Data/bin/obj/node_modules/samples/secrets/local logs.
Fix upload document pagination and study source visibility

- Page through personal uploads and expose retry actions
- Preserve prose answers containing lists and restore legacy study sources
-->

<!-- Document management UI commit proposal: client/src/features/study/StudyPage.tsx, client/src/features/documents/UploadPage.tsx, client/src/features/documents/api.ts, client/src/lib/query-keys.ts, TASKS.md. Stage only task hunks; exclude unrelated prior changes, App_Data/bin/obj/node_modules/samples/secrets.
Add study document selection and personal upload management

- Show a consistent document selector and paginated personal uploads
- Confirm document deletion and refresh affected source lists
-->

<!-- Duplicate ownership and confirmation UI commit proposal: Features/Documents/DocumentEndpoints.cs, UploadedDocumentConfiguration.cs, ScopeDocumentDuplicatesToOwner migration + AppDbContextModelSnapshot.cs, client/src/lib/use-confirm.ts, components/common/ConfirmProvider.tsx, app/providers.tsx, features/documents/UploadPage.tsx, features/battle/MyRoomsPage.tsx and BattleRoomPage.tsx, TASKS.md. Stage only task hunks; preserve earlier work and exclude App_Data/bin/obj/node_modules/samples/secrets/logs. Database-only named UNEC ownership restoration is complete and must not be generalized to other users or files.
Scope duplicate uploads to owners and replace browser confirmations

- Allow independent personal uploads and preserve document ownership
- Use accessible in-app dialogs for confirmation actions
-->

<!-- Structured Study commit proposal: src/ExamFlow.Api/Features/Study/StudyEndpoints.cs, StudyAiClient.cs, StudyPracticeDtos.cs, StudyPracticeEndpoints.cs; client/src/features/study/api.ts, StudyPage.tsx; TASKS.md. Stage only this task’s hunks (Study files already contain prior uncommitted work); exclude other modules, App_Data/bin/obj/node_modules/samples/secrets.
Replace free study chat with document-based practice modes

- Reuse source questions and cached difficulty-based AI exercises
- Persist practice results and weak topics in existing Study tables
-->
