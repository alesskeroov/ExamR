START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    ALTER TABLE "UserUsageLimits" ADD "MonthlyDimTests" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE TABLE "BillingSettings" (
        "Id" integer NOT NULL,
        "Enabled" boolean NOT NULL,
        "WhatsAppNumber" character varying(20),
        "InstagramUsername" character varying(30),
        "PaymentInstructions" character varying(1000),
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedByUserId" uuid,
        CONSTRAINT "PK_BillingSettings" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_BillingSettings_Single" CHECK ("Id" = 1)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE TABLE "Plans" (
        "Id" uuid NOT NULL,
        "Name" character varying(40) NOT NULL,
        "Description" character varying(300),
        "Price" numeric(10,2) NOT NULL,
        "DurationDays" integer NOT NULL,
        "IsFree" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "IsFeatured" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "MonthlyUploads" integer,
        "MonthlyExams" integer,
        "MonthlyBattles" integer,
        "MonthlyDimTests" integer,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedByUserId" uuid,
        CONSTRAINT "PK_Plans" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Plans_DurationDays" CHECK ("DurationDays" BETWEEN 1 AND 365),
        CONSTRAINT "CK_Plans_MonthlyBattles" CHECK ("MonthlyBattles" IS NULL OR "MonthlyBattles" BETWEEN 0 AND 100000),
        CONSTRAINT "CK_Plans_MonthlyDimTests" CHECK ("MonthlyDimTests" IS NULL OR "MonthlyDimTests" BETWEEN 0 AND 100000),
        CONSTRAINT "CK_Plans_MonthlyExams" CHECK ("MonthlyExams" IS NULL OR "MonthlyExams" BETWEEN 0 AND 100000),
        CONSTRAINT "CK_Plans_MonthlyUploads" CHECK ("MonthlyUploads" IS NULL OR "MonthlyUploads" BETWEEN 0 AND 100000),
        CONSTRAINT "CK_Plans_Price" CHECK ("Price" >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE TABLE "PaymentRequests" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "PlanId" uuid NOT NULL,
        "Code" character varying(12) NOT NULL,
        "Periods" integer NOT NULL,
        "Amount" numeric(10,2) NOT NULL,
        "Channel" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "AdminNote" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "ReviewedAt" timestamp with time zone,
        "ReviewedByUserId" uuid,
        CONSTRAINT "PK_PaymentRequests" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_PaymentRequests_Periods" CHECK ("Periods" BETWEEN 1 AND 12),
        CONSTRAINT "FK_PaymentRequests_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PaymentRequests_Plans_PlanId" FOREIGN KEY ("PlanId") REFERENCES "Plans" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE TABLE "Subscriptions" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "PlanId" uuid NOT NULL,
        "StartsAt" timestamp with time zone NOT NULL,
        "EndsAt" timestamp with time zone NOT NULL,
        "Source" character varying(20) NOT NULL,
        "PaymentRequestId" uuid,
        "Note" character varying(500),
        "CancelledAt" timestamp with time zone,
        "CancelledByUserId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedByUserId" uuid,
        CONSTRAINT "PK_Subscriptions" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Subscriptions_Period" CHECK ("EndsAt" > "StartsAt"),
        CONSTRAINT "FK_Subscriptions_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Subscriptions_PaymentRequests_PaymentRequestId" FOREIGN KEY ("PaymentRequestId") REFERENCES "PaymentRequests" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_Subscriptions_Plans_PlanId" FOREIGN KEY ("PlanId") REFERENCES "Plans" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    INSERT INTO "BillingSettings" ("Id", "Enabled", "InstagramUsername", "PaymentInstructions", "UpdatedAt", "UpdatedByUserId", "WhatsAppNumber")
    VALUES (1, FALSE, NULL, NULL, TIMESTAMPTZ '2026-11-16T00:00:00+00:00', NULL, NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    INSERT INTO "Plans" ("Id", "Description", "DurationDays", "IsActive", "IsFeatured", "IsFree", "MonthlyBattles", "MonthlyDimTests", "MonthlyExams", "MonthlyUploads", "Name", "Price", "SortOrder", "UpdatedAt", "UpdatedByUserId")
    VALUES ('6a1f0c3e-0000-4000-8000-000000000001', 'Saytı sınamaq üçün', 30, TRUE, FALSE, TRUE, 0, 0, 3, 1, 'Free', 0.0, 0, TIMESTAMPTZ '2026-11-16T00:00:00+00:00', NULL);
    INSERT INTO "Plans" ("Id", "Description", "DurationDays", "IsActive", "IsFeatured", "IsFree", "MonthlyBattles", "MonthlyDimTests", "MonthlyExams", "MonthlyUploads", "Name", "Price", "SortOrder", "UpdatedAt", "UpdatedByUserId")
    VALUES ('6a1f0c3e-0000-4000-8000-000000000002', 'İmtahandan əvvəlki 7 gün üçün', 7, TRUE, FALSE, FALSE, 5, 5, 30, 3, 'İmtahan həftəsi', 3.0, 1, TIMESTAMPTZ '2026-11-16T00:00:00+00:00', NULL);
    INSERT INTO "Plans" ("Id", "Description", "DurationDays", "IsActive", "IsFeatured", "IsFree", "MonthlyBattles", "MonthlyDimTests", "MonthlyExams", "MonthlyUploads", "Name", "Price", "SortOrder", "UpdatedAt", "UpdatedByUserId")
    VALUES ('6a1f0c3e-0000-4000-8000-000000000003', 'Müntəzəm hazırlıq üçün', 30, TRUE, FALSE, FALSE, 10, 10, 60, 10, 'Standart', 5.0, 2, TIMESTAMPTZ '2026-11-16T00:00:00+00:00', NULL);
    INSERT INTO "Plans" ("Id", "Description", "DurationDays", "IsActive", "IsFeatured", "IsFree", "MonthlyBattles", "MonthlyDimTests", "MonthlyExams", "MonthlyUploads", "Name", "Price", "SortOrder", "UpdatedAt", "UpdatedByUserId")
    VALUES ('6a1f0c3e-0000-4000-8000-000000000004', 'Ən çox seçilən paket', 30, TRUE, TRUE, FALSE, 30, 30, 150, 25, 'Pro', 7.0, 3, TIMESTAMPTZ '2026-11-16T00:00:00+00:00', NULL);
    INSERT INTO "Plans" ("Id", "Description", "DurationDays", "IsActive", "IsFeatured", "IsFree", "MonthlyBattles", "MonthlyDimTests", "MonthlyExams", "MonthlyUploads", "Name", "Price", "SortOrder", "UpdatedAt", "UpdatedByUserId")
    VALUES ('6a1f0c3e-0000-4000-8000-000000000005', 'Limitsiz istifadə', 30, TRUE, FALSE, FALSE, NULL, NULL, NULL, NULL, 'Premium', 10.0, 4, TIMESTAMPTZ '2026-11-16T00:00:00+00:00', NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    ALTER TABLE "UserUsageLimits" ADD CONSTRAINT "CK_UserUsageLimits_MonthlyDimTests" CHECK ("MonthlyDimTests" IS NULL OR "MonthlyDimTests" BETWEEN 0 AND 100000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE UNIQUE INDEX "IX_PaymentRequests_Code" ON "PaymentRequests" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE INDEX "IX_PaymentRequests_PlanId" ON "PaymentRequests" ("PlanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE INDEX "IX_PaymentRequests_Status_CreatedAt" ON "PaymentRequests" ("Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE UNIQUE INDEX "IX_PaymentRequests_UserId" ON "PaymentRequests" ("UserId") WHERE "Status" = 'Pending';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE UNIQUE INDEX "IX_Plans_IsFree" ON "Plans" ("IsFree") WHERE "IsFree";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE INDEX "IX_Subscriptions_PaymentRequestId" ON "Subscriptions" ("PaymentRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE INDEX "IX_Subscriptions_PlanId" ON "Subscriptions" ("PlanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    CREATE INDEX "IX_Subscriptions_UserId_EndsAt" ON "Subscriptions" ("UserId", "EndsAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116195531_AddPlansAndSubscriptions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261116195531_AddPlansAndSubscriptions', '9.0.21');
    END IF;
END $EF$;
COMMIT;

