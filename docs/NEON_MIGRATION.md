# Azure SQL → Neon PostgreSQL köçürməsi

Səbəb: Azure SQL serverless pulsuz limiti (100 000 vCore-saniyə ≈ 41 oyaq saat/ay, 60 dəq auto-pause) real
istifadəyə çatmır. Neon pulsuz planı: 100 CU-saat/ay (0,25 CU ilə ≈ 400 saat), 5 dəq sonra yatır, oyanma
bir neçə yüz ms, 1 GB yer, 6 saatlıq PITR.

Branch: `feature/neon-postgres`. Bu sənəd production keçidini addım-addım təsvir edir. Hər addım istifadəçinin
təsdiqi ilə edilir; heç bir addım Azure bazasını dəyişmir (mənbə yalnız oxunur).

## Kodda nə dəyişdi

- Provider: `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4 (`UseNpgsql`, retry açıqdır).
- `sp_getapplock` → `pg_advisory_xact_lock` (AI büdcəsi, Battle rank) və `pg_try_advisory_lock` (ExplanationWorker,
  session lock — **pooled olmayan** Neon endpoint-i tələb edir).
- `byte[]` row version-lar (`[Timestamp]`/`IsRowVersion`) `xmin` sistem sütununa map olunur (`AppDbContext`), API
  tokenləri dəyişmir.
- Sual/variant mətni ICU `az-x-icu` collation-ı ilə saxlanır; axtarış `ILIKE` (I/ı, İ/i düzgün).
- SQL Server migration-ları `Infrastructure/Data/Migrations.SqlServer/`-də arxivdədir (build-ə daxil deyil);
  yeni baza `Migrations/…_InitialPostgres`.
- Köçürmə aləti: `scripts/migrate-to-postgres` (solution-a daxil deyil).

## Lokal yoxlama (2026-11-07)

Docker `postgres:17`, port 5433. Smoke: qeydiyyat/giriş, PDF (669 sual), axtarış, imtahan (paralel cavablar,
aktiv imtahan unique index-i), qruplar, sual redaktəsi (`xmin` concurrency), iki eyni vaxtlı Battle (nəticə,
reytinq, rank lock). Köçürmə: lokal SQL Server `ExamFlow` → Postgres, 42 cədvəl / 32 288 sətir, 10 s, bütün saylar
uyğun; köçürülmüş imtahan nəticələri (jsonb snapshot), `uuid[]` sahələri və yeni imtahan API-dən işləyir.
Yoxlanmayıb: AI büdcə kilidi, ExplanationWorker, Learning/Tutor axınları.

## Production keçidi

1. **Neon layihəsi** (istifadəçi): region `AWS Europe (Frankfurt) aws-eu-central-1`, Postgres 17. Paneldən
   **direct** connection string götürün (host-da `-pooler` yoxdur) — tətbiq və köçürmə üçün bunu istifadə edin.
   Npgsql formatı: `Host=ep-….eu-central-1.aws.neon.tech;Database=neondb;Username=…;Password=…;SSL Mode=Require;Timeout=30`.
2. **ICU yoxlaması** (Neon SQL Editor):
   `select 'hansı' collate "az-x-icu" ilike 'HANSI';` → `t` olmalıdır.
3. **Sxem**: `ConnectionStrings__Default=<Neon direct>` ilə
   `dotnet ef database update --project src/ExamFlow.Api` (yalnız `InitialPostgres`).
4. **Yazıları dayandırın**: Render-də servisi suspend edin (və ya maintenance mode). Köçürmə zamanı yeni yazı olmamalıdır.
5. **Köçürmə** (Azure oyanacaq — bir neçə dəqiqəlik vCore xərci):
   ```bash
   SOURCE_SQLSERVER=<Azure connection string> TARGET_POSTGRES=<Neon direct> dotnet run --project scripts/migrate-to-postgres
   ```
   Alət hədəf boş deyilsə və ya mənbə `20261107121520_AddExamAnswerLock` migration-ında deyilsə dayanır; hər şey bir
   tranzaksiyadadır və sonda sətir saylarını müqayisə edir (`-- --verify` yalnız müqayisə edir).
6. **Render**: `ConnectionStrings__Default` = Neon direct; `feature/neon-postgres` merge olunduqdan sonra deploy;
   servisi resume edin. Yoxlama: `/api/health` (DB), giriş, imtahan, Battle.
7. **Geri qayıtma**: Render env-i köhnə Azure dəyərinə qaytarın və `main`-in Postgres-dən əvvəlki commit-ini deploy
   edin. Azure bazası ən azı 2 həftə toxunulmaz saxlanır (pauzada, pulsuz).

## Neon pulsuz plan qeydləri

- Backup: yalnız 6 saat PITR + 1 manual snapshot. Dövri `pg_dump` (məs. GitHub Actions) ayrıca qurulmalıdır.
- Limit: 100 CU-saat bitəndə compute ay sonuna qədər dayanır (məlumat qalır). Neon panelində gündəlik izləyin.
- Max compute-u 0,25 CU-da saxlamaq büdcəni maksimum uzadır; autoscale CU-saatı daha tez xərcləyir.
