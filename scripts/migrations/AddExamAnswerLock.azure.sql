BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107121520_AddExamAnswerLock'
)
BEGIN
    ALTER TABLE [Exams] ADD [LockAfterCorrect] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107121520_AddExamAnswerLock'
)
BEGIN
    ALTER TABLE [ExamQuestions] ADD [LockedAt] datetimeoffset(3) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107121520_AddExamAnswerLock'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107121520_AddExamAnswerLock', N'9.0.21');
END;

COMMIT;
GO

