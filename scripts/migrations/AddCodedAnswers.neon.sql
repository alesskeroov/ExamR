START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddCodedAnswers') THEN
    ALTER TABLE "Questions" ADD "CodedAnswer" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddCodedAnswers') THEN
    ALTER TABLE "ExamQuestions" ADD "AnswerText" character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddCodedAnswers') THEN
    ALTER TABLE "ExamQuestions" ADD "CorrectAnswer" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddCodedAnswers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261114130000_AddCodedAnswers', '9.0.21');
    END IF;
END $EF$;
COMMIT;

