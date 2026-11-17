START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    ALTER TABLE "UserUsageLimits" ADD "MonthlyGuides" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    ALTER TABLE "Plans" ADD "MonthlyGuides" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    UPDATE "Plans" SET "MonthlyGuides" = 0
    WHERE "Id" NOT IN ('6a1f0c3e-0000-4000-8000-000000000001', '6a1f0c3e-0000-4000-8000-000000000002',
        '6a1f0c3e-0000-4000-8000-000000000003', '6a1f0c3e-0000-4000-8000-000000000004', '6a1f0c3e-0000-4000-8000-000000000005');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    UPDATE "Plans" SET "MonthlyGuides" = 0
    WHERE "Id" = '6a1f0c3e-0000-4000-8000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    UPDATE "Plans" SET "MonthlyGuides" = 3
    WHERE "Id" = '6a1f0c3e-0000-4000-8000-000000000002';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    UPDATE "Plans" SET "MonthlyGuides" = 0
    WHERE "Id" = '6a1f0c3e-0000-4000-8000-000000000003';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    UPDATE "Plans" SET "MonthlyGuides" = 10
    WHERE "Id" = '6a1f0c3e-0000-4000-8000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    UPDATE "Plans" SET "MonthlyGuides" = NULL
    WHERE "Id" = '6a1f0c3e-0000-4000-8000-000000000005';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    ALTER TABLE "UserUsageLimits" ADD CONSTRAINT "CK_UserUsageLimits_MonthlyGuides" CHECK ("MonthlyGuides" IS NULL OR "MonthlyGuides" BETWEEN 0 AND 100000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    ALTER TABLE "Plans" ADD CONSTRAINT "CK_Plans_MonthlyGuides" CHECK ("MonthlyGuides" IS NULL OR "MonthlyGuides" BETWEEN 0 AND 100000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117140857_AddGuideLimit') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261117140857_AddGuideLimit', '9.0.21');
    END IF;
END $EF$;
COMMIT;

