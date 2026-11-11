START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111124727_AddFeedbackMessages') THEN
    CREATE TABLE "FeedbackMessages" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "RequestId" uuid NOT NULL,
        "Kind" character varying(16) NOT NULL,
        "Text" character varying(1500) NOT NULL,
        "SenderName" character varying(100) NOT NULL,
        "SenderEmail" character varying(256) NOT NULL,
        "DocumentName" character varying(255),
        "PagePath" character varying(256) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "NextAttemptAt" timestamp with time zone NOT NULL,
        "SentAt" timestamp with time zone,
        "Attempts" integer NOT NULL,
        "LeaseId" uuid,
        CONSTRAINT "PK_FeedbackMessages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_FeedbackMessages_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111124727_AddFeedbackMessages') THEN
    CREATE INDEX "IX_FeedbackMessages_NextAttemptAt" ON "FeedbackMessages" ("NextAttemptAt") WHERE "SentAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111124727_AddFeedbackMessages') THEN
    CREATE UNIQUE INDEX "IX_FeedbackMessages_UserId_RequestId" ON "FeedbackMessages" ("UserId", "RequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111124727_AddFeedbackMessages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111124727_AddFeedbackMessages', '9.0.21');
    END IF;
END $EF$;
COMMIT;
