START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115100000_AddDimAudio') THEN
    CREATE TABLE "DimAudioPlays" (
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
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115100000_AddDimAudio') THEN
    CREATE TABLE "DimAudios" (
        "Id" uuid NOT NULL,
        "DocumentId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "QuestionFrom" integer NOT NULL,
        "QuestionTo" integer NOT NULL,
        "StorageKey" character varying(100) NOT NULL,
        "ContentType" character varying(50) NOT NULL,
        "SizeBytes" bigint NOT NULL,
        "MaxPlays" integer,
        "UploadedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL,
        CONSTRAINT "PK_DimAudios" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_DimAudios_Range" CHECK ("QuestionFrom" >= 1 AND "QuestionTo" >= "QuestionFrom"),
        CONSTRAINT "FK_DimAudios_UploadedDocuments_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "UploadedDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115100000_AddDimAudio') THEN
    CREATE INDEX "IX_DimAudios_DocumentId" ON "DimAudios" ("DocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115100000_AddDimAudio') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115100000_AddDimAudio', '9.0.21');
    END IF;
END $EF$;
COMMIT;

