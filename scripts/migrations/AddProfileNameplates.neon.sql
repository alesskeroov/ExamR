START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110082129_AddProfileNameplates') THEN
    ALTER TABLE "AspNetUsers" ADD "Nameplate" character varying(4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110082129_AddProfileNameplates') THEN
    ALTER TABLE "AspNetUsers" ADD "NameplateGrants" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110082129_AddProfileNameplates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110082129_AddProfileNameplates', '9.0.21');
    END IF;
END $EF$;
COMMIT;
