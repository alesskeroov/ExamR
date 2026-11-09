START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108193733_AddLearningCardStates') THEN
    CREATE TABLE "LearningCardStates" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "LearningDocumentId" uuid NOT NULL,
        "QuestionHash" character varying(64) NOT NULL,
        "Box" integer NOT NULL,
        "Reviews" integer NOT NULL,
        "Lapses" integer NOT NULL,
        "LastGrade" character varying(8) NOT NULL,
        "DueAt" timestamp with time zone NOT NULL,
        "LastReviewedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LearningCardStates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LearningCardStates_LearningDocuments_LearningDocumentId" FOREIGN KEY ("LearningDocumentId") REFERENCES "LearningDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108193733_AddLearningCardStates') THEN
    CREATE INDEX "IX_LearningCardStates_LearningDocumentId" ON "LearningCardStates" ("LearningDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108193733_AddLearningCardStates') THEN
    CREATE UNIQUE INDEX "IX_LearningCardStates_UserId_LearningDocumentId_QuestionHash" ON "LearningCardStates" ("UserId", "LearningDocumentId", "QuestionHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108193733_AddLearningCardStates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108193733_AddLearningCardStates', '9.0.21');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108200052_ReplaceCardScheduleWithMastery') THEN
    ALTER TABLE "LearningCardStates" DROP COLUMN "DueAt";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108200052_ReplaceCardScheduleWithMastery') THEN
    ALTER TABLE "LearningCardStates" RENAME COLUMN "Box" TO "Streak";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108200052_ReplaceCardScheduleWithMastery') THEN
    ALTER TABLE "LearningCardStates" ADD "Mastered" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108200052_ReplaceCardScheduleWithMastery') THEN
    UPDATE "LearningCardStates" SET "Streak" = CASE WHEN "LastGrade" = 'Good' THEN 1 ELSE 0 END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108200052_ReplaceCardScheduleWithMastery') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108200052_ReplaceCardScheduleWithMastery', '9.0.21');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109111617_AddPersonalCards') THEN
    CREATE TABLE "LearningCardOverrides" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "LearningDocumentId" uuid NOT NULL,
        "CardId" character varying(64) NOT NULL,
        "Front" character varying(500),
        "Back" character varying(4000),
        "Hidden" boolean NOT NULL,
        "Deleted" boolean NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LearningCardOverrides" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LearningCardOverrides_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_LearningCardOverrides_LearningDocuments_LearningDocumentId" FOREIGN KEY ("LearningDocumentId") REFERENCES "LearningDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109111617_AddPersonalCards') THEN
    CREATE TABLE "LearningUserCards" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "LearningDocumentId" uuid NOT NULL,
        "TopicKey" character varying(64),
        "Front" character varying(500) NOT NULL,
        "Back" character varying(4000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LearningUserCards" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LearningUserCards_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_LearningUserCards_LearningDocuments_LearningDocumentId" FOREIGN KEY ("LearningDocumentId") REFERENCES "LearningDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109111617_AddPersonalCards') THEN
    CREATE INDEX "IX_LearningCardOverrides_LearningDocumentId" ON "LearningCardOverrides" ("LearningDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109111617_AddPersonalCards') THEN
    CREATE UNIQUE INDEX "IX_LearningCardOverrides_UserId_LearningDocumentId_CardId" ON "LearningCardOverrides" ("UserId", "LearningDocumentId", "CardId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109111617_AddPersonalCards') THEN
    CREATE INDEX "IX_LearningUserCards_LearningDocumentId" ON "LearningUserCards" ("LearningDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109111617_AddPersonalCards') THEN
    CREATE INDEX "IX_LearningUserCards_UserId_LearningDocumentId" ON "LearningUserCards" ("UserId", "LearningDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109111617_AddPersonalCards') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261109111617_AddPersonalCards', '9.0.21');
    END IF;
END $EF$;
COMMIT;

