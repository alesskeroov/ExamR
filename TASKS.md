# TASKS.md — ExamFlow

Agentlər (Claude Code, OpenAI Codex) arasında ötürmə faylı. Qaydalar: `AGENTS.md` §6.
Hər tapşırıq bitəndə yenilənir. Tarixlər: YYYY-MM-DD.

## Davam edən iş / Handoff

_Hazırda yarımçıq iş yoxdur._

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

| Tarix | İş | Commit |
|---|---|---|
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
6. **Battle təkmilləşdirmələri:** host lobby-də parametrləri dəyişə bilsin; host yarış zamanı ayrılanda hostluğun ötürülməsi; chat-ın DB-də saxlanması (lazım olsa); bir neçə server üçün Redis backplane (yalnız miqyas lazım olanda).
7. **Production deploy:** SPA-nı `wwwroot`-a köçürmək, `MapFallbackToFile`, env dəyişənləri (`docs/ARCHITECTURE.md` §4).

## Məlum problemlər / məhdudiyyətlər

- Token 60 dəq-dir, refresh token yoxdur (bax: Növbəti addımlar #1). Token `localStorage`-dadır.
- Real SMTP serveri ilə test edilməyib; lokalda emaillər `src/ExamFlow.Api/App_Data/mail/*.eml`.
- Rate limit yaddaşdadır və IP üzrədir; Vite proxy arxasında bütün sorğular eyni IP sayılır.
- Parser yalnız mətn əsaslı PDF-lər üçündür (OCR yoxdur); hərfin iki dəfə çəkilməsi ilə yaradılan "fake bold" tanınmır. Real formatlardan yalnız UNEC yoxlanılıb.
- `samples/*.pdf` Git-də deyil → başqa kompüterdə `RealSampleTests` heç nə yoxlamadan keçir; Azərbaycan şrift testi Windows Arial tələb edir.
- SQL Server-də Full-Text Search quraşdırılmayıb; gələcək axtarış `LIKE` + `Azeri_Latin_100_CI_AS` ilə.
- Köhnə ("bütün PDF-lər") imtahanlarda aralıq yoxdur: tarixçədə "Bütün PDF-lər" göstərilir, "Yenidən imtahan ver" gizlədilir.
- Admin üçün UI yoxdur; ilk Admin yalnız `Seed:Admin:*` konfiqurasiyası ilə yaranır.
- Battle otaqları və chat yalnız yaddaşdadır (bir server): API restart olanda aktiv otaqlar itir; bitmiş yarışların nəticələri DB-də qalır. Boş lobby 15 dəq, bitmiş otaq 30 dəq sonra silinir.
- Battle-a yalnız düzgün cavabı məlum olan suallar düşür; yarış başladıqdan sonra yeni iştirakçı qoşula bilməz (yalnız reconnect).
- Lokal bazada test hesabları qalıb (`*@examflow.test`) — istəsəniz silin.
