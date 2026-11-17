-- Read-only: which EF migrations this database already has. Run BEFORE ExamR.pending-migrations.azure.sql
-- to see what will be applied (every migration of `main` not listed here), and AFTER it to confirm.
-- Expected last row after applying: 20261104133507_AddQuestionVisuals
SELECT [MigrationId], [ProductVersion]
FROM [__EFMigrationsHistory]
ORDER BY [MigrationId];

-- Rows from other branches (e.g. 20261104..._AddQuestionExplanations from feature/explanation-bank) are not
-- touched by the script.
