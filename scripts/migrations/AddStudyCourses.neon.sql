START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddStudyCourses') THEN
    CREATE TABLE "StudyCourses" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "ExamDate" date,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_StudyCourses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StudyCourses_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddStudyCourses') THEN
    CREATE TABLE "StudyCourseDocuments" (
        "CourseId" uuid NOT NULL,
        "DocumentId" uuid NOT NULL,
        "Role" character varying(16) NOT NULL,
        "Position" integer NOT NULL,
        CONSTRAINT "PK_StudyCourseDocuments" PRIMARY KEY ("CourseId", "DocumentId"),
        CONSTRAINT "FK_StudyCourseDocuments_StudyCourses_CourseId" FOREIGN KEY ("CourseId") REFERENCES "StudyCourses" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StudyCourseDocuments_UploadedDocuments_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "UploadedDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddStudyCourses') THEN
    CREATE INDEX "IX_StudyCourseDocuments_DocumentId" ON "StudyCourseDocuments" ("DocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddStudyCourses') THEN
    CREATE INDEX "IX_StudyCourses_UserId" ON "StudyCourses" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114130000_AddStudyCourses') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261114130000_AddStudyCourses', '9.0.21');
    END IF;
END $EF$;
COMMIT;

