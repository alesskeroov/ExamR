START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107213313_AddDimTests') THEN
    CREATE TABLE "DimBankQuestions" (
        "QuestionId" uuid NOT NULL,
        "Track" character varying(20) NOT NULL,
        "Subject" character varying(10) NOT NULL,
        "Topic" character varying(200),
        "ApprovedByUserId" uuid NOT NULL,
        "ApprovedAt" timestamp(3) with time zone NOT NULL,
        CONSTRAINT "PK_DimBankQuestions" PRIMARY KEY ("QuestionId"),
        CONSTRAINT "FK_DimBankQuestions_Questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "Questions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107213313_AddDimTests') THEN
    CREATE TABLE "DimTests" (
        "Id" uuid NOT NULL,
        "Track" character varying(20) NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "GroupCode" character varying(10),
        "Title" character varying(200) NOT NULL,
        "DurationMinutes" smallint NOT NULL,
        "AttemptLimit" smallint,
        "AvailableFrom" timestamp(3) with time zone,
        "AvailableTo" timestamp(3) with time zone,
        "IsPublished" boolean NOT NULL,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL,
        "Sections" jsonb NOT NULL,
        CONSTRAINT "PK_DimTests" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107213313_AddDimTests') THEN
    CREATE TABLE "DimAttempts" (
        "ExamId" uuid NOT NULL,
        "TestId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "StartedAt" timestamp(3) with time zone NOT NULL,
        "Items" jsonb NOT NULL,
        CONSTRAINT "PK_DimAttempts" PRIMARY KEY ("ExamId"),
        CONSTRAINT "FK_DimAttempts_DimTests_TestId" FOREIGN KEY ("TestId") REFERENCES "DimTests" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DimAttempts_Exams_ExamId" FOREIGN KEY ("ExamId") REFERENCES "Exams" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107213313_AddDimTests') THEN
    CREATE INDEX "IX_DimAttempts_TestId_UserId" ON "DimAttempts" ("TestId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107213313_AddDimTests') THEN
    CREATE INDEX "IX_DimBankQuestions_Track_Subject" ON "DimBankQuestions" ("Track", "Subject");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107213313_AddDimTests') THEN
    CREATE INDEX "IX_DimTests_Track_IsPublished" ON "DimTests" ("Track", "IsPublished");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107213313_AddDimTests') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107213313_AddDimTests', '9.0.21');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108145156_MarkDimSourceDocuments') THEN
    ALTER TABLE "UploadedDocuments" ADD "IsDimSource" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108145156_MarkDimSourceDocuments') THEN
    UPDATE "UploadedDocuments" SET "IsDimSource" = TRUE
    WHERE "Id" IN (SELECT q."DocumentId" FROM "DimBankQuestions" b JOIN "Questions" q ON q."Id" = b."QuestionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108145156_MarkDimSourceDocuments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108145156_MarkDimSourceDocuments', '9.0.21');
    END IF;
END $EF$;
COMMIT;

