START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115183846_AddQuestionPassages') THEN
    CREATE TABLE "QuestionPassages" (
        "Id" uuid NOT NULL,
        "DocumentId" uuid NOT NULL,
        "FromNumber" integer NOT NULL,
        "ToNumber" integer NOT NULL,
        "Title" character varying(300),
        "Text" character varying(20000) NOT NULL,
        "CreatedAt" timestamp(3) with time zone NOT NULL,
        "UpdatedAt" timestamp(3) with time zone NOT NULL,
        CONSTRAINT "PK_QuestionPassages" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_QuestionPassages_Range" CHECK ("FromNumber" >= 1 AND "ToNumber" >= "FromNumber"),
        CONSTRAINT "FK_QuestionPassages_UploadedDocuments_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "UploadedDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115183846_AddQuestionPassages') THEN
    CREATE INDEX "IX_QuestionPassages_DocumentId_FromNumber" ON "QuestionPassages" ("DocumentId", "FromNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115183846_AddQuestionPassages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115183846_AddQuestionPassages', '9.0.21');
    END IF;
END $EF$;
COMMIT;

