START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115174715_AddCardSchedule') THEN
    ALTER TABLE "LearningCardStates" ADD "DueAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115174715_AddCardSchedule') THEN
    ALTER TABLE "LearningCardStates" ADD "IntervalDays" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115174715_AddCardSchedule') THEN
    CREATE INDEX "IX_LearningCardStates_UserId_DueAt" ON "LearningCardStates" ("UserId", "DueAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115174715_AddCardSchedule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115174715_AddCardSchedule', '9.0.21');
    END IF;
END $EF$;
COMMIT;

