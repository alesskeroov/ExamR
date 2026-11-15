START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115200000_AddDimAudioPlayLimit') THEN
    ALTER TABLE "DimAudios" ADD COLUMN IF NOT EXISTS "MaxPlays" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115200000_AddDimAudioPlayLimit') THEN
    CREATE TABLE IF NOT EXISTS "DimAudioPlays" (
        "ExamId" uuid NOT NULL,
        "AudioId" uuid NOT NULL,
        "Count" integer NOT NULL,
        CONSTRAINT "PK_DimAudioPlays" PRIMARY KEY ("ExamId", "AudioId"),
        CONSTRAINT "FK_DimAudioPlays_Exams_ExamId" FOREIGN KEY ("ExamId") REFERENCES "Exams" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115200000_AddDimAudioPlayLimit') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115200000_AddDimAudioPlayLimit', '9.0.21');
    END IF;
END $EF$;
COMMIT;

