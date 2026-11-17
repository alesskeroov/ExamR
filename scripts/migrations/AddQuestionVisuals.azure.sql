-- ExamR Visual Questions (Phase 3): Azure SQL deployment script for migration 20261104133507_AddQuestionVisuals.
-- Select the intended application database before executing this entire batch.
-- Adds only the new table dbo.QuestionVisuals (+ 2 indexes, 1 check constraint). No existing table or row is changed.
-- Image files are NOT stored here: rows only hold the storage key (files go to R2/S3 via the app).
-- Prerequisite: tables UploadedDocuments and Questions exist (initial schema). Does not apply older migrations.
-- Safe to rerun: a second run changes nothing. Generated from `dotnet ef migrations script --idempotent`,
-- wrapped with checks in one transaction (all or nothing).
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51000, 'Migration history is missing. Apply the existing schema migrations first.', 1;

    IF OBJECT_ID(N'dbo.UploadedDocuments', N'U') IS NULL OR OBJECT_ID(N'dbo.Questions', N'U') IS NULL
        THROW 51001, 'UploadedDocuments/Questions tables are missing. Verify the selected database.', 1;

    IF OBJECT_ID(N'dbo.QuestionVisuals', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
           WHERE [MigrationId] = N'20261104133507_AddQuestionVisuals')
        THROW 51002, 'QuestionVisuals exists without its migration history entry. Review schema drift before proceeding.', 1;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261104133507_AddQuestionVisuals'
    )
    BEGIN
        CREATE TABLE [QuestionVisuals] (
            [Id] uniqueidentifier NOT NULL,
            [DocumentId] uniqueidentifier NOT NULL,
            [QuestionId] uniqueidentifier NULL,
            [OptionPosition] tinyint NULL,
            [SortOrder] int NOT NULL,
            [PageNumber] int NOT NULL,
            [Left] float NOT NULL,
            [Bottom] float NOT NULL,
            [Width] float NOT NULL,
            [Height] float NOT NULL,
            [MappingStatus] varchar(20) NOT NULL,
            [Note] nvarchar(200) NULL,
            [StorageKey] varchar(120) NULL,
            [ContentType] varchar(50) NULL,
            [ByteSize] int NOT NULL,
            [PixelWidth] int NOT NULL,
            [PixelHeight] int NOT NULL,
            [Sha256] binary(32) NOT NULL,
            [CreatedAt] datetimeoffset(3) NOT NULL,
            CONSTRAINT [PK_QuestionVisuals] PRIMARY KEY ([Id]),
            CONSTRAINT [CK_QuestionVisuals_OptionPosition] CHECK ([OptionPosition] IS NULL OR [OptionPosition] BETWEEN 0 AND 5),
            CONSTRAINT [FK_QuestionVisuals_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]),
            CONSTRAINT [FK_QuestionVisuals_UploadedDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [UploadedDocuments] ([Id]) ON DELETE CASCADE
        );

        CREATE INDEX [IX_QuestionVisuals_DocumentId_SortOrder] ON [QuestionVisuals] ([DocumentId], [SortOrder]);
        CREATE INDEX [IX_QuestionVisuals_QuestionId] ON [QuestionVisuals] ([QuestionId]);

        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261104133507_AddQuestionVisuals', N'9.0.21');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Verification (read-only): expect 1 history row, 19 columns, 2 indexes, 1 check constraint.
SELECT [MigrationId], [ProductVersion] FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261104133507_AddQuestionVisuals';
SELECT COUNT(*) AS [Columns] FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.QuestionVisuals');
SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.QuestionVisuals') AND [name] LIKE N'IX_%';
SELECT [name] FROM sys.check_constraints WHERE [parent_object_id] = OBJECT_ID(N'dbo.QuestionVisuals');
GO

-- Rollback (only if needed, before any images were processed): uncomment and run.
-- BEGIN TRANSACTION;
-- DROP TABLE [QuestionVisuals];
-- DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261104133507_AddQuestionVisuals';
-- COMMIT;
