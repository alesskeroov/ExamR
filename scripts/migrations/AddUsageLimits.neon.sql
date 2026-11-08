START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108154058_AddUsageLimits') THEN
    ALTER TABLE "LearningAiUsage" ADD "CountsTowardsMonthlyQuota" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108154058_AddUsageLimits') THEN
    CREATE TABLE "UsageEvents" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "ResourceId" uuid,
        CONSTRAINT "PK_UsageEvents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UsageEvents_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108154058_AddUsageLimits') THEN
    CREATE TABLE "UserUsageLimits" (
        "UserId" uuid NOT NULL,
        "MonthlyUploads" integer,
        "MonthlyExams" integer,
        "MonthlyBattles" integer,
        "MonthlyAiRequests" integer,
        "Unlimited" boolean NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedByUserId" uuid,
        CONSTRAINT "PK_UserUsageLimits" PRIMARY KEY ("UserId"),
        CONSTRAINT "CK_UserUsageLimits_MonthlyAiRequests" CHECK ("MonthlyAiRequests" IS NULL OR "MonthlyAiRequests" BETWEEN 0 AND 100000),
        CONSTRAINT "CK_UserUsageLimits_MonthlyBattles" CHECK ("MonthlyBattles" IS NULL OR "MonthlyBattles" BETWEEN 0 AND 100000),
        CONSTRAINT "CK_UserUsageLimits_MonthlyExams" CHECK ("MonthlyExams" IS NULL OR "MonthlyExams" BETWEEN 0 AND 100000),
        CONSTRAINT "CK_UserUsageLimits_MonthlyUploads" CHECK ("MonthlyUploads" IS NULL OR "MonthlyUploads" BETWEEN 0 AND 100000),
        CONSTRAINT "FK_UserUsageLimits_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108154058_AddUsageLimits') THEN
    CREATE INDEX "IX_UsageEvents_ResourceId_Kind" ON "UsageEvents" ("ResourceId", "Kind");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108154058_AddUsageLimits') THEN
    CREATE INDEX "IX_UsageEvents_UserId_Kind_OccurredAt" ON "UsageEvents" ("UserId", "Kind", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108154058_AddUsageLimits') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108154058_AddUsageLimits', '9.0.21');
    END IF;
END $EF$;
COMMIT;
