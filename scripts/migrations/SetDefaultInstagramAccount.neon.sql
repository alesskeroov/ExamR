START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117102747_SetDefaultInstagramAccount') THEN
    UPDATE "BillingSettings" SET "InstagramUsername" = 'examr_az'
    WHERE "Id" = 1 AND "InstagramUsername" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117102747_SetDefaultInstagramAccount') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261117102747_SetDefaultInstagramAccount', '9.0.21');
    END IF;
END $EF$;
COMMIT;

