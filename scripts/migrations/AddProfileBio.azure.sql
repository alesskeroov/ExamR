-- ExamR Profile Phase 1: Azure SQL deployment script.
-- Select the intended application database before executing this entire batch.
-- Prerequisite: migrations through AddDataProtectionKeys must already be applied.
-- Adds only nullable AspNetUsers.Bio (160 characters); preserves existing data.
-- Safe to rerun after successful application. Does not apply older migrations.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51000, 'Migration history is missing. Apply the existing schema migrations first.', 1;

    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102080000_AddDataProtectionKeys')
        THROW 51001, 'Required baseline AddDataProtectionKeys is not applied. No changes were made.', 1;

    IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
        THROW 51002, 'AspNetUsers table is missing. Verify the selected database.', 1;

    IF COL_LENGTH(N'dbo.AspNetUsers', N'Bio') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
           WHERE [MigrationId] = N'20261102165917_AddProfileBio')
        THROW 51003, 'Bio exists without its migration history entry. Review schema drift before proceeding.', 1;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102165917_AddProfileBio'
    )
    BEGIN
        ALTER TABLE [AspNetUsers] ADD [Bio] nvarchar(160) NULL;
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102165917_AddProfileBio'
    )
    BEGIN
        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261102165917_AddProfileBio', N'9.0.21');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Verification: expect the migration ID and a nullable nvarchar column of 160 characters.
SELECT [MigrationId], [ProductVersion]
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261102165917_AddProfileBio';

SELECT [COLUMN_NAME], [DATA_TYPE], [CHARACTER_MAXIMUM_LENGTH], [IS_NULLABLE]
FROM [INFORMATION_SCHEMA].[COLUMNS]
WHERE [TABLE_SCHEMA] = N'dbo' AND [TABLE_NAME] = N'AspNetUsers' AND [COLUMN_NAME] = N'Bio';
