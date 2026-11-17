-- ExamR Azure SQL deployment script. Select the application database first.
-- Required baseline: 20261102080000_AddDataProtectionKeys. Existing rows are preserved.
-- Run the entire file as one batch. Safe to repeat after successful application.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51000, 'Migration history is missing. Verify the selected database.', 1;
    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261102080000_AddDataProtectionKeys')
        THROW 51001, 'Required baseline 20261102080000_AddDataProtectionKeys is not applied. No changes were made.', 1;
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

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        ALTER TABLE [BattleResults] ADD [CorrectTimeMs] bigint NOT NULL DEFAULT CAST(0 AS bigint);
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        ALTER TABLE [BattleResults] ADD [RatingAfter] int NULL;
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        ALTER TABLE [BattleResults] ADD [RatingBefore] int NULL;
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        ALTER TABLE [BattleResults] ADD [RatingOutcome] int NULL;
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        ALTER TABLE [AspNetUsers] ADD [DeletedAt] datetimeoffset NULL;
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        ALTER TABLE [AspNetUsers] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        CREATE TABLE [BattleRatings] (
            [UserId] uniqueidentifier NOT NULL,
            [CurrentRating] int NOT NULL,
            [HighestRating] int NOT NULL,
            [TotalRankedBattles] int NOT NULL,
            [Wins] int NOT NULL,
            [Losses] int NOT NULL,
            [Draws] int NOT NULL,
            [LastRatingUpdate] datetimeoffset NOT NULL,
            [Version] rowversion NOT NULL,
            CONSTRAINT [PK_BattleRatings] PRIMARY KEY ([UserId]),
            CONSTRAINT [FK_BattleRatings_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
        );
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        CREATE INDEX [IX_BattleRatings_CurrentRating_Wins] ON [BattleRatings] ([CurrentRating] DESC, [Wins] DESC);
    END;

    IF NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261102173012_AddBattleRanksAndAccountLifecycle'
    )
    BEGIN
        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261102173012_AddBattleRanksAndAccountLifecycle', N'9.0.21');
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT [MigrationId], [ProductVersion] FROM [__EFMigrationsHistory]
WHERE [MigrationId] IN (N'20261102165917_AddProfileBio', N'20261102173012_AddBattleRanksAndAccountLifecycle');
