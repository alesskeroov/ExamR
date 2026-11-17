IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [UploadedDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [StoredFileName] varchar(64) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [Sha256] binary(32) NOT NULL,
        [PageCount] int NULL,
        [Status] varchar(20) NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset(3) NOT NULL,
        [UpdatedAt] datetimeoffset(3) NOT NULL,
        [ProcessedAt] datetimeoffset(3) NULL,
        [ProcessingReport] nvarchar(max) NULL,
        CONSTRAINT [PK_UploadedDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UploadedDocuments_AspNetUsers_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [Questions] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [SourceNumber] int NOT NULL,
        [Text] nvarchar(4000) COLLATE Azeri_Latin_100_CI_AS NOT NULL,
        [ContentHash] binary(32) NOT NULL,
        [VerificationStatus] varchar(20) NOT NULL,
        [ParseWarning] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset(3) NOT NULL,
        [UpdatedAt] datetimeoffset(3) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Questions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Questions_UploadedDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [UploadedDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE TABLE [AnswerOptions] (
        [Id] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [Position] tinyint NOT NULL,
        [Label] char(1) NOT NULL,
        [Text] nvarchar(1000) COLLATE Azeri_Latin_100_CI_AS NOT NULL,
        [IsCorrect] bit NOT NULL,
        CONSTRAINT [PK_AnswerOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AnswerOptions_Position] CHECK ([Position] BETWEEN 0 AND 5),
        CONSTRAINT [FK_AnswerOptions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AnswerOptions_QuestionId_Position] ON [AnswerOptions] ([QuestionId], [Position]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AnswerOptions_SingleCorrect] ON [AnswerOptions] ([QuestionId]) WHERE [IsCorrect] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Questions_ContentHash] ON [Questions] ([ContentHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Questions_DocumentId_SourceNumber] ON [Questions] ([DocumentId], [SourceNumber]) INCLUDE ([VerificationStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UploadedDocuments_Status] ON [UploadedDocuments] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UploadedDocuments_UploadedByUserId] ON [UploadedDocuments] ([UploadedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_UploadedDocuments_Sha256] ON [UploadedDocuments] ([Sha256]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101151118_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101151118_InitialCreate', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160123_AddExams'
)
BEGIN
    CREATE TABLE [Exams] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [QuestionCount] smallint NOT NULL,
        [RevealMode] varchar(20) NOT NULL,
        [SourceDocumentIds] nvarchar(max) NOT NULL,
        [Status] varchar(20) NOT NULL,
        [CompletionReason] varchar(20) NULL,
        [StartedAt] datetimeoffset(3) NOT NULL,
        [ExpiresAt] datetimeoffset(3) NOT NULL,
        [CompletedAt] datetimeoffset(3) NULL,
        [CorrectCount] smallint NULL,
        [IncorrectCount] smallint NULL,
        [UnansweredCount] smallint NULL,
        [UngradedCount] smallint NULL,
        [ScorePercent] decimal(5,2) NULL,
        CONSTRAINT [PK_Exams] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Exams_QuestionCount] CHECK ([QuestionCount] BETWEEN 1 AND 200),
        CONSTRAINT [FK_Exams_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160123_AddExams'
)
BEGIN
    CREATE TABLE [ExamQuestions] (
        [Id] uniqueidentifier NOT NULL,
        [ExamId] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NULL,
        [Position] smallint NOT NULL,
        [CorrectOptionId] uniqueidentifier NULL,
        [SelectedOptionId] uniqueidentifier NULL,
        [AnsweredAt] datetimeoffset(3) NULL,
        [Outcome] varchar(20) NULL,
        [Snapshot] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ExamQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExamQuestions_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExamQuestions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160123_AddExams'
)
BEGIN
    CREATE INDEX [IX_ExamQuestions_QuestionId] ON [ExamQuestions] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160123_AddExams'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ExamQuestions_ExamId_Position] ON [ExamQuestions] ([ExamId], [Position]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160123_AddExams'
)
BEGIN
    CREATE INDEX [IX_Exams_UserId_StartedAt] ON [Exams] ([UserId], [StartedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160123_AddExams'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Exams_UserActive] ON [Exams] ([UserId]) WHERE [Status] = ''InProgress''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101160123_AddExams'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101160123_AddExams', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101162745_AddExamQuestionRange'
)
BEGIN
    ALTER TABLE [Exams] ADD [RangeFrom] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101162745_AddExamQuestionRange'
)
BEGIN
    ALTER TABLE [Exams] ADD [RangeTo] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101162745_AddExamQuestionRange'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101162745_AddExamQuestionRange', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101170144_AddBattleResults'
)
BEGIN
    CREATE TABLE [BattleResults] (
        [Id] uniqueidentifier NOT NULL,
        [BattleId] uniqueidentifier NOT NULL,
        [RoomCode] varchar(8) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [RangeFrom] int NOT NULL,
        [RangeTo] int NOT NULL,
        [QuestionCount] int NOT NULL,
        [ParticipantCount] int NOT NULL,
        [Rank] int NOT NULL,
        [Score] int NOT NULL,
        [CorrectCount] int NOT NULL,
        [PlayedAt] datetimeoffset(3) NOT NULL,
        CONSTRAINT [PK_BattleResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BattleResults_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101170144_AddBattleResults'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BattleResults_BattleId_UserId] ON [BattleResults] ([BattleId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101170144_AddBattleResults'
)
BEGIN
    CREATE INDEX [IX_BattleResults_UserId_PlayedAt] ON [BattleResults] ([UserId], [PlayedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101170144_AddBattleResults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101170144_AddBattleResults', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE TABLE [Friendships] (
        [UserLowId] uniqueidentifier NOT NULL,
        [UserHighId] uniqueidentifier NOT NULL,
        [RequestedById] uniqueidentifier NOT NULL,
        [IsAccepted] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Friendships] PRIMARY KEY ([UserLowId], [UserHighId]),
        CONSTRAINT [FK_Friendships_AspNetUsers_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Friendships_AspNetUsers_UserHighId] FOREIGN KEY ([UserHighId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Friendships_AspNetUsers_UserLowId] FOREIGN KEY ([UserLowId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE TABLE [SocialNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [RoomCode] varchar(6) NULL,
        [HostName] nvarchar(100) NULL,
        [DocumentName] nvarchar(512) NULL,
        [QuestionCount] int NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NULL,
        [ReadAt] datetimeoffset NULL,
        CONSTRAINT [PK_SocialNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SocialNotifications_AspNetUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SocialNotifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE TABLE [UserBlocks] (
        [UserId] uniqueidentifier NOT NULL,
        [BlockedUserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserBlocks] PRIMARY KEY ([UserId], [BlockedUserId]),
        CONSTRAINT [FK_UserBlocks_AspNetUsers_BlockedUserId] FOREIGN KEY ([BlockedUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserBlocks_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE INDEX [IX_Friendships_RequestedById] ON [Friendships] ([RequestedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE INDEX [IX_Friendships_UserHighId] ON [Friendships] ([UserHighId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE INDEX [IX_SocialNotifications_ActorId] ON [SocialNotifications] ([ActorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE INDEX [IX_SocialNotifications_UserId_CreatedAt] ON [SocialNotifications] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE INDEX [IX_SocialNotifications_UserId_ReadAt] ON [SocialNotifications] ([UserId], [ReadAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    CREATE INDEX [IX_UserBlocks_BlockedUserId] ON [UserBlocks] ([BlockedUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101175749_AddFriendsAndNotifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101175749_AddFriendsAndNotifications', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101182620_AddPersistentBattleRooms'
)
BEGIN
    CREATE TABLE [StoredBattleRooms] (
        [Id] uniqueidentifier NOT NULL,
        [Code] varchar(6) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [HostUserId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [DocumentName] nvarchar(512) NOT NULL,
        [RangeFrom] int NOT NULL,
        [RangeTo] int NOT NULL,
        [QuestionCount] int NOT NULL,
        [SecondsPerQuestion] int NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [IsClosed] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [StateJson] nvarchar(max) NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_StoredBattleRooms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StoredBattleRooms_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StoredBattleRooms_AspNetUsers_HostUserId] FOREIGN KEY ([HostUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101182620_AddPersistentBattleRooms'
)
BEGIN
    CREATE TABLE [StoredBattleMembers] (
        [RoomId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [IsKicked] bit NOT NULL,
        CONSTRAINT [PK_StoredBattleMembers] PRIMARY KEY ([RoomId], [UserId]),
        CONSTRAINT [FK_StoredBattleMembers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StoredBattleMembers_StoredBattleRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [StoredBattleRooms] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101182620_AddPersistentBattleRooms'
)
BEGIN
    CREATE INDEX [IX_StoredBattleMembers_UserId] ON [StoredBattleMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101182620_AddPersistentBattleRooms'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StoredBattleRooms_Code] ON [StoredBattleRooms] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101182620_AddPersistentBattleRooms'
)
BEGIN
    CREATE INDEX [IX_StoredBattleRooms_CreatedById] ON [StoredBattleRooms] ([CreatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101182620_AddPersistentBattleRooms'
)
BEGIN
    CREATE INDEX [IX_StoredBattleRooms_HostUserId_Status] ON [StoredBattleRooms] ([HostUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101182620_AddPersistentBattleRooms'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101182620_AddPersistentBattleRooms', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101190000_AddUserProfiles'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [Nickname] nvarchar(24) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101190000_AddUserProfiles'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [NormalizedNickname] nvarchar(24) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101190000_AddUserProfiles'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [AvatarFileName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101190000_AddUserProfiles'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [AvatarContentType] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101190000_AddUserProfiles'
)
BEGIN
    UPDATE AspNetUsers SET Nickname = 'user' + LEFT(REPLACE(CONVERT(varchar(36), Id), '-', ''), 16), NormalizedNickname = 'USER' + LEFT(REPLACE(CONVERT(varchar(36), Id), '-', ''), 16)
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101190000_AddUserProfiles'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AspNetUsers_NormalizedNickname] ON [AspNetUsers] ([NormalizedNickname]) WHERE [NormalizedNickname] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101190000_AddUserProfiles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101190000_AddUserProfiles', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101192906_AddSmartStudy'
)
BEGIN
    ALTER TABLE [Questions] ADD [Explanation] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101192906_AddSmartStudy'
)
BEGIN
    CREATE TABLE [StudyAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [Prompt] nvarchar(4000) NOT NULL,
        [OptionsJson] nvarchar(max) NOT NULL,
        [CorrectIndex] int NULL,
        [Answer] nvarchar(max) NULL,
        [Score] int NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [DueAt] datetimeoffset NULL,
        CONSTRAINT [PK_StudyAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudyAttempts_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_StudyAttempts_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101192906_AddSmartStudy'
)
BEGIN
    CREATE INDEX [IX_StudyAttempts_QuestionId] ON [StudyAttempts] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101192906_AddSmartStudy'
)
BEGIN
    CREATE INDEX [IX_StudyAttempts_UserId_QuestionId_CompletedAt] ON [StudyAttempts] ([UserId], [QuestionId], [CompletedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101192906_AddSmartStudy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101192906_AddSmartStudy', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101200105_AddStudyAi'
)
BEGIN
    ALTER TABLE [StudyAttempts] ADD [EvaluationLeaseUntil] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101200105_AddStudyAi'
)
BEGIN
    ALTER TABLE [StudyAttempts] ADD [ExpectedAnswer] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101200105_AddStudyAi'
)
BEGIN
    ALTER TABLE [StudyAttempts] ADD [ReferenceText] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101200105_AddStudyAi'
)
BEGIN
    CREATE TABLE [StudyAiTurns] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [ReplyJson] nvarchar(max) NULL,
        [Model] nvarchar(100) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [InputTokens] int NOT NULL,
        [CachedInputTokens] int NOT NULL,
        [OutputTokens] int NOT NULL,
        [EstimatedCostUsd] decimal(18,8) NOT NULL,
        [IsEstimated] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StudyAiTurns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudyAiTurns_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101200105_AddStudyAi'
)
BEGIN
    CREATE INDEX [IX_StudyAiTurns_UserId_CreatedAt] ON [StudyAiTurns] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101200105_AddStudyAi'
)
BEGIN
    CREATE INDEX [IX_StudyAiTurns_UserId_DocumentId_CreatedAt] ON [StudyAiTurns] ([UserId], [DocumentId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101200105_AddStudyAi'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101200105_AddStudyAi', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102010000_ScopeDocumentDuplicatesToOwner'
)
BEGIN
    DROP INDEX [UX_UploadedDocuments_Sha256] ON [UploadedDocuments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102010000_ScopeDocumentDuplicatesToOwner'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_UploadedDocuments_User_Sha256] ON [UploadedDocuments] ([UploadedByUserId], [Sha256]) WHERE [UploadedByUserId] IS NOT NULL AND [Sha256] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102010000_ScopeDocumentDuplicatesToOwner'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102010000_ScopeDocumentDuplicatesToOwner', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102030000_AddStudyGroups'
)
BEGIN
    CREATE TABLE [StudyGroups] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [NormalizedName] nvarchar(120) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StudyGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102030000_AddStudyGroups'
)
BEGIN
    CREATE TABLE [StudyGroupMembers] (
        [GroupId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_StudyGroupMembers] PRIMARY KEY ([GroupId], [UserId]),
        CONSTRAINT [FK_StudyGroupMembers_StudyGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [StudyGroups] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StudyGroupMembers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102030000_AddStudyGroups'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudyGroups_NormalizedName] ON [StudyGroups] ([NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102030000_AddStudyGroups'
)
BEGIN
    CREATE INDEX [IX_StudyGroupMembers_UserId] ON [StudyGroupMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102030000_AddStudyGroups'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102030000_AddStudyGroups', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102040000_AddDocumentSharing'
)
BEGIN
    ALTER TABLE [UploadedDocuments] ADD [ShareWithEveryone] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102040000_AddDocumentSharing'
)
BEGIN
    CREATE TABLE [DocumentUserShares] (
        [DocumentId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DocumentUserShares] PRIMARY KEY ([DocumentId], [UserId]),
        CONSTRAINT [FK_DocumentUserShares_UploadedDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [UploadedDocuments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DocumentUserShares_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102040000_AddDocumentSharing'
)
BEGIN
    CREATE TABLE [DocumentGroupShares] (
        [DocumentId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DocumentGroupShares] PRIMARY KEY ([DocumentId], [GroupId]),
        CONSTRAINT [FK_DocumentGroupShares_UploadedDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [UploadedDocuments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DocumentGroupShares_StudyGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [StudyGroups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102040000_AddDocumentSharing'
)
BEGIN
    CREATE INDEX [IX_DocumentUserShares_UserId] ON [DocumentUserShares] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102040000_AddDocumentSharing'
)
BEGIN
    CREATE INDEX [IX_DocumentGroupShares_GroupId] ON [DocumentGroupShares] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102040000_AddDocumentSharing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102040000_AddDocumentSharing', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102050000_AddAdminRoleAudits'
)
BEGIN
    CREATE TABLE [AdminRoleAudits] (
        [Id] bigint NOT NULL IDENTITY,
        [ActorId] uniqueidentifier NOT NULL,
        [TargetUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(16) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AdminRoleAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102050000_AddAdminRoleAudits'
)
BEGIN
    CREATE INDEX [IX_AdminRoleAudits_TargetUserId] ON [AdminRoleAudits] ([TargetUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102050000_AddAdminRoleAudits'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102050000_AddAdminRoleAudits', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102060000_AddQuestionBookmarks'
)
BEGIN
    CREATE TABLE [QuestionBookmarks] (
        [UserId] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_QuestionBookmarks] PRIMARY KEY ([UserId], [QuestionId]),
        CONSTRAINT [FK_QuestionBookmarks_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuestionBookmarks_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102060000_AddQuestionBookmarks'
)
BEGIN
    CREATE INDEX [IX_QuestionBookmarks_QuestionId] ON [QuestionBookmarks] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102060000_AddQuestionBookmarks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102060000_AddQuestionBookmarks', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070000_AddFocusSessions'
)
BEGIN
    CREATE TABLE [FocusSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Activity] nvarchar(16) NOT NULL,
        [PlannedMinutes] int NOT NULL,
        [FocusSeconds] int NOT NULL,
        [Answered] int NOT NULL,
        [Correct] int NOT NULL,
        [Completed] bit NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [EndedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FocusSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FocusSessions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070000_AddFocusSessions'
)
BEGIN
    CREATE INDEX [IX_FocusSessions_UserId_EndedAt] ON [FocusSessions] ([UserId], [EndedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070000_AddFocusSessions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102070000_AddFocusSessions', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080000_AddDataProtectionKeys'
)
BEGIN
    CREATE TABLE [DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [Xml] nvarchar(max) NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102080000_AddDataProtectionKeys'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102080000_AddDataProtectionKeys', N'9.0.21');
END;

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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    CREATE TABLE [LearningDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Format] nvarchar(8) NOT NULL,
        [PageCount] int NULL,
        [ParagraphCount] int NOT NULL,
        [CharacterCount] bigint NOT NULL,
        [ChunkCount] int NOT NULL,
        [LooksScanned] bit NOT NULL,
        [Error] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ProcessedAt] datetimeoffset NULL,
        CONSTRAINT [PK_LearningDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningDocuments_UploadedDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [UploadedDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    CREATE TABLE [LearningDocumentChunks] (
        [Id] uniqueidentifier NOT NULL,
        [LearningDocumentId] uniqueidentifier NOT NULL,
        [Ordinal] int NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [TokenEstimate] int NOT NULL,
        [PageFrom] int NULL,
        [PageTo] int NULL,
        [Section] nvarchar(200) NULL,
        [ParagraphFrom] int NOT NULL,
        [ParagraphTo] int NOT NULL,
        CONSTRAINT [PK_LearningDocumentChunks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningDocumentChunks_LearningDocuments_LearningDocumentId] FOREIGN KEY ([LearningDocumentId]) REFERENCES [LearningDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    CREATE TABLE [LearningProcessingJobs] (
        [Id] uniqueidentifier NOT NULL,
        [LearningDocumentId] uniqueidentifier NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Attempt] int NOT NULL,
        [Error] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [StartedAt] datetimeoffset NULL,
        [FinishedAt] datetimeoffset NULL,
        CONSTRAINT [PK_LearningProcessingJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningProcessingJobs_LearningDocuments_LearningDocumentId] FOREIGN KEY ([LearningDocumentId]) REFERENCES [LearningDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LearningDocumentChunks_LearningDocumentId_Ordinal] ON [LearningDocumentChunks] ([LearningDocumentId], [Ordinal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LearningDocuments_DocumentId] ON [LearningDocuments] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    CREATE INDEX [IX_LearningProcessingJobs_LearningDocumentId] ON [LearningProcessingJobs] ([LearningDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    CREATE INDEX [IX_LearningProcessingJobs_Status] ON [LearningProcessingJobs] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102181712_AddLearningDocuments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102181712_AddLearningDocuments', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE TABLE [LearningAiUsage] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NULL,
        [MessageId] uniqueidentifier NULL,
        [Model] nvarchar(64) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [InputTokens] int NOT NULL,
        [CachedInputTokens] int NOT NULL,
        [OutputTokens] int NOT NULL,
        [ReservedUsd] decimal(18,6) NOT NULL,
        [CostUsd] decimal(18,6) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_LearningAiUsage] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE TABLE [LearningChatSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LearningDocumentId] uniqueidentifier NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LearningChatSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningChatSessions_LearningDocuments_LearningDocumentId] FOREIGN KEY ([LearningDocumentId]) REFERENCES [LearningDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE TABLE [LearningChatMessages] (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [Role] nvarchar(16) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Grounded] bit NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LearningChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningChatMessages_LearningChatSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [LearningChatSessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE TABLE [LearningMessageSources] (
        [Id] uniqueidentifier NOT NULL,
        [MessageId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [ChunkOrdinal] int NOT NULL,
        [PageFrom] int NULL,
        [PageTo] int NULL,
        [Section] nvarchar(200) NULL,
        [Excerpt] nvarchar(400) NOT NULL,
        [Score] float NOT NULL,
        [Cited] bit NOT NULL,
        CONSTRAINT [PK_LearningMessageSources] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningMessageSources_LearningChatMessages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [LearningChatMessages] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE INDEX [IX_LearningAiUsage_Status] ON [LearningAiUsage] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE INDEX [IX_LearningAiUsage_UserId_CreatedAt] ON [LearningAiUsage] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE INDEX [IX_LearningChatMessages_SessionId_CreatedAt] ON [LearningChatMessages] ([SessionId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE INDEX [IX_LearningChatSessions_LearningDocumentId] ON [LearningChatSessions] ([LearningDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE INDEX [IX_LearningChatSessions_UserId_UpdatedAt] ON [LearningChatSessions] ([UserId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    CREATE INDEX [IX_LearningMessageSources_MessageId] ON [LearningMessageSources] ([MessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102184008_AddLearningChat'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102184008_AddLearningChat', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190601_AddLearningTopicProgress'
)
BEGIN
    CREATE TABLE [LearningTopicProgress] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LearningDocumentId] uniqueidentifier NOT NULL,
        [TopicKey] nvarchar(32) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CurrentStep] int NOT NULL,
        [StepCount] int NOT NULL,
        [Attempts] int NOT NULL,
        [Correct] int NOT NULL,
        [Partial] int NOT NULL,
        [AwaitingAnswer] bit NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LearningTopicProgress] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningTopicProgress_LearningDocuments_LearningDocumentId] FOREIGN KEY ([LearningDocumentId]) REFERENCES [LearningDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190601_AddLearningTopicProgress'
)
BEGIN
    CREATE INDEX [IX_LearningTopicProgress_LearningDocumentId] ON [LearningTopicProgress] ([LearningDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190601_AddLearningTopicProgress'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LearningTopicProgress_UserId_LearningDocumentId_TopicKey] ON [LearningTopicProgress] ([UserId], [LearningDocumentId], [TopicKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102190601_AddLearningTopicProgress'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102190601_AddLearningTopicProgress', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    CREATE TABLE [LearningQuizItems] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LearningDocumentId] uniqueidentifier NOT NULL,
        [Question] nvarchar(1000) NOT NULL,
        [ReferenceAnswer] nvarchar(2000) NOT NULL,
        [KeyPointsJson] nvarchar(2000) NOT NULL,
        [ChunkOrdinal] int NOT NULL,
        [PageFrom] int NULL,
        [PageTo] int NULL,
        [UserAnswer] nvarchar(4000) NULL,
        [Status] nvarchar(16) NOT NULL,
        [Feedback] nvarchar(1000) NULL,
        [MissingPointsJson] nvarchar(2000) NULL,
        [AiChecked] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [AnsweredAt] datetimeoffset NULL,
        CONSTRAINT [PK_LearningQuizItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningQuizItems_LearningDocuments_LearningDocumentId] FOREIGN KEY ([LearningDocumentId]) REFERENCES [LearningDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    CREATE TABLE [LearningMaterials] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LearningDocumentId] uniqueidentifier NOT NULL,
        [DocumentSha256] varbinary(32) NOT NULL,
        [Kind] nvarchar(16) NOT NULL,
        [Length] nvarchar(8) NULL,
        [Minutes] int NULL,
        [QuestionNumbersJson] nvarchar(4000) NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [CacheKey] nvarchar(64) NOT NULL,
        [PromptVersion] nvarchar(16) NOT NULL,
        [Saved] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LearningMaterials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LearningMaterials_LearningDocuments_LearningDocumentId] FOREIGN KEY ([LearningDocumentId]) REFERENCES [LearningDocuments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    CREATE INDEX [IX_LearningQuizItems_LearningDocumentId] ON [LearningQuizItems] ([LearningDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    CREATE INDEX [IX_LearningQuizItems_UserId_LearningDocumentId_CreatedAt] ON [LearningQuizItems] ([UserId], [LearningDocumentId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    CREATE INDEX [IX_LearningMaterials_CacheKey] ON [LearningMaterials] ([CacheKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    CREATE INDEX [IX_LearningMaterials_LearningDocumentId] ON [LearningMaterials] ([LearningDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    CREATE INDEX [IX_LearningMaterials_UserId_LearningDocumentId_CreatedAt] ON [LearningMaterials] ([UserId], [LearningDocumentId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103155545_AddLearningTutor2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103155545_AddLearningTutor2', N'9.0.21');
END;

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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104133507_AddQuestionVisuals'
)
BEGIN
    CREATE INDEX [IX_QuestionVisuals_DocumentId_SortOrder] ON [QuestionVisuals] ([DocumentId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104133507_AddQuestionVisuals'
)
BEGIN
    CREATE INDEX [IX_QuestionVisuals_QuestionId] ON [QuestionVisuals] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104133507_AddQuestionVisuals'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104133507_AddQuestionVisuals', N'9.0.21');
END;

COMMIT;
GO

