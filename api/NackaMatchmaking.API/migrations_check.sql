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
CREATE TABLE [AdminUsers] (
    [Id] uniqueidentifier NOT NULL,
    [Username] nvarchar(max) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AdminUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [ChatMessages] (
    [Id] uniqueidentifier NOT NULL,
    [MatchId] uniqueidentifier NOT NULL,
    [SenderId] uniqueidentifier NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id])
);

CREATE TABLE [Participants] (
    [Id] uniqueidentifier NOT NULL,
    [Email] nvarchar(450) NULL,
    [Title] nvarchar(max) NULL,
    [HasAcceptedTerms] bit NOT NULL,
    [Firstname] nvarchar(max) NULL,
    [Lastname] nvarchar(max) NULL,
    [Organization] nvarchar(max) NULL,
    [Bio] nvarchar(max) NULL,
    [Photo] nvarchar(max) NULL,
    [Superpower] nvarchar(max) NULL,
    [SuperpowerOther] nvarchar(max) NULL,
    [Challenge] nvarchar(max) NULL,
    [ChallengeOther] nvarchar(max) NULL,
    [Topics] nvarchar(max) NULL,
    [TopicsOther] nvarchar(max) NULL,
    [CompanyDescription] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Participants] PRIMARY KEY ([Id])
);

CREATE TABLE [Questions] (
    [Id] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Placeholder] nvarchar(max) NULL,
    [MaxLength] int NULL,
    [IsHidden] bit NOT NULL,
    [Order] int NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Questions] PRIMARY KEY ([Id])
);

CREATE TABLE [Registrations] (
    [Id] uniqueidentifier NOT NULL,
    [Firstname] nvarchar(max) NULL,
    [Lastname] nvarchar(max) NULL,
    [Organization] nvarchar(max) NULL,
    [Title] nvarchar(max) NULL,
    [HasAcceptedTerms] bit NOT NULL,
    [Email] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Registrations] PRIMARY KEY ([Id])
);

CREATE TABLE [Settings] (
    [Key] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Settings] PRIMARY KEY ([Key])
);

CREATE TABLE [UserAnswers] (
    [Id] int NOT NULL IDENTITY,
    [ParticipantId] uniqueidentifier NOT NULL,
    [QuestionId] nvarchar(max) NOT NULL,
    [AnswerValue] nvarchar(max) NOT NULL,
    [OtherValue] nvarchar(max) NULL,
    CONSTRAINT [PK_UserAnswers] PRIMARY KEY ([Id])
);

CREATE TABLE [Matches] (
    [Id] uniqueidentifier NOT NULL,
    [User1Id] uniqueidentifier NOT NULL,
    [User2Id] uniqueidentifier NOT NULL,
    [User1Interested] bit NOT NULL,
    [User2Interested] bit NOT NULL,
    [User1Feedback] int NULL,
    [User1FeedbackReason] nvarchar(max) NULL,
    [User2Feedback] int NULL,
    [User2FeedbackReason] nvarchar(max) NULL,
    [Score] int NOT NULL,
    [Status] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [MatchedAt] datetime2 NULL,
    CONSTRAINT [PK_Matches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Matches_Participants_User1Id] FOREIGN KEY ([User1Id]) REFERENCES [Participants] ([Id]),
    CONSTRAINT [FK_Matches_Participants_User2Id] FOREIGN KEY ([User2Id]) REFERENCES [Participants] ([Id])
);

CREATE TABLE [QuestionOptions] (
    [Id] int NOT NULL IDENTITY,
    [QuestionId] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [Icon] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Order] int NOT NULL,
    [IsHidden] bit NOT NULL,
    CONSTRAINT [PK_QuestionOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QuestionOptions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description', N'IsHidden', N'MaxLength', N'Order', N'Placeholder', N'Title', N'Type') AND [object_id] = OBJECT_ID(N'[Questions]'))
    SET IDENTITY_INSERT [Questions] ON;
INSERT INTO [Questions] ([Id], [Description], [IsHidden], [MaxLength], [Order], [Placeholder], [Title], [Type])
VALUES (N'q1', N'Inom vilket område kan du bidra med mest värde till andra?', CAST(0 AS bit), NULL, 1, NULL, N'Min Superkraft', N'MultipleChoice'),
(N'q2', N'Vad är den största utmaningen du vill lösa just nu?', CAST(0 AS bit), NULL, 2, NULL, N'Min Utmaning', N'Choice'),
(N'q3', N'Vad snackar du helst om vid kaffemaskinen?', CAST(0 AS bit), NULL, 3, NULL, N'Samtalsämnen', N'MultipleChoice'),
(N'q4', N'Beskriv ditt mål för i år med en mening. AI:n använder detta för att hitta dolda kopplingar.', CAST(0 AS bit), 200, 4, N'T.ex. Jag vill expandera min konsultverksamhet till norra Europa...', N'Kort om dig', N'Text'),
(N'q5', N'Beskriv företaget där du jobbar, max en mening 50 tkn.', CAST(0 AS bit), 50, 5, NULL, N'Kort om företaget', N'Text');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description', N'IsHidden', N'MaxLength', N'Order', N'Placeholder', N'Title', N'Type') AND [object_id] = OBJECT_ID(N'[Questions]'))
    SET IDENTITY_INSERT [Questions] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'Value') AND [object_id] = OBJECT_ID(N'[Settings]'))
    SET IDENTITY_INSERT [Settings] ON;
INSERT INTO [Settings] ([Key], [Value])
VALUES (N'ProfileDescription', N'Kontrollera att dina uppgifter stämmer så att andra kan hitta dig på mässan.'),
(N'ProfileTitle', N'Mina uppgifter'),
(N'SurveyOpen', N'true'),
(N'WelcomeButton', N'Starta matchningen'),
(N'WelcomeDescription', N'Välkommen till årets matchmaking-event!'),
(N'WelcomeTagline', N'Matchmaking 2026'),
(N'WelcomeTitle', N'Välkommen!');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'Value') AND [object_id] = OBJECT_ID(N'[Settings]'))
    SET IDENTITY_INSERT [Settings] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description', N'Icon', N'IsHidden', N'Order', N'QuestionId', N'Title', N'Value') AND [object_id] = OBJECT_ID(N'[QuestionOptions]'))
    SET IDENTITY_INSERT [QuestionOptions] ON;
INSERT INTO [QuestionOptions] ([Id], [Description], [Icon], [IsHidden], [Order], [QuestionId], [Title], [Value])
VALUES (1, N'Hjälper andra att växa och sälja mer.', N'🚀', CAST(0 AS bit), 1, N'q1', N'Försäljning & BD', N'sales'),
(2, N'Expert på digitala lösningar och AI.', N'💻', CAST(0 AS bit), 2, N'q1', N'Teknik & IT', N'tech'),
(3, N'Bygger organisationer och team.', N'🎯', CAST(0 AS bit), 3, N'q1', N'Strategi & Ledarskap', N'strat'),
(4, N'Drivkraft för grön omställning.', N'🌿', CAST(0 AS bit), 4, N'q1', N'Hållbarhet', N'sust'),
(5, N'Skriv din egen superkraft.', N'✨', CAST(0 AS bit), 5, N'q1', N'Annat...', N'other'),
(6, N'Söker leads och nya marknader.', N'📈', CAST(0 AS bit), 1, N'q2', N'Hitta nya kunder', N'leads'),
(7, N'Söker strategiska samarbeten.', N'🤝', CAST(0 AS bit), 2, N'q2', N'Nätverk & Partners', N'partners'),
(8, N'Behöver stärka upp teamet.', N'🌟', CAST(0 AS bit), 3, N'q2', N'Rekrytera talang', N'talent'),
(9, N'Söker investering eller stöd.', N'💰', CAST(0 AS bit), 4, N'q2', N'Kapital & Finansiering', N'funding'),
(10, N'Berätta om din utmaning.', N'🏢', CAST(0 AS bit), 5, N'q2', N'Annat...', N'other'),
(11, N'Möjligheter och hot.', N'🤖', CAST(0 AS bit), 1, N'q3', N'AI-revolutionen', N'ai'),
(12, N'Lokal tillväxt och miljö.', N'📍', CAST(0 AS bit), 2, N'q3', N'Nackas framtid', N'local'),
(13, N'Hybridarbete och kultur.', N'🛸', CAST(0 AS bit), 3, N'q3', N'Framtidens jobb', N'future'),
(14, N'Vad brinner du för?', N'☕', CAST(0 AS bit), 4, N'q3', N'Annat...', N'other'),
(15, N'Hittar rätt bolag och ser till att de ökar i värde', N'💰', CAST(0 AS bit), 6, N'q1', N'Kapital & Investering', N'invest'),
(16, N'Hjälper människor att må bättre och prestera mer.', N'🧘', CAST(0 AS bit), 7, N'q1', N'Hälsa & Friskvård', N'health'),
(17, N'Skapar arbetsplatser där människor trivs och presterar bättre.', N'🏢', CAST(0 AS bit), 8, N'q1', N'Lokaler & Arbetsplatser', N'facility'),
(18, N'Att hitta rätt mark och lokaler för att din verksamhet ska kunna växa.', N'📍', CAST(0 AS bit), 6, N'q2', N'Lokal & Mark', N'facility'),
(19, N'Att nå fram i bruset och förvandla kontakter till betalande kunder.', N'📣', CAST(0 AS bit), 7, N'q2', N'Sälj & Marknadsföring', N'sales'),
(20, N'Maximera resursutnyttjandet.', N'♻️', CAST(0 AS bit), 5, N'q3', N'Cirkulär ekonomi', N'circular'),
(21, N'Hitta nästa växel snabbt.', N'🌱', CAST(0 AS bit), 6, N'q3', N'Tillväxt', N'growth'),
(22, N'Skala upp hållbart imperium.', N'🏛️', CAST(0 AS bit), 7, N'q3', N'”Romarriket”', N'rome');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Description', N'Icon', N'IsHidden', N'Order', N'QuestionId', N'Title', N'Value') AND [object_id] = OBJECT_ID(N'[QuestionOptions]'))
    SET IDENTITY_INSERT [QuestionOptions] OFF;

CREATE INDEX [IX_Matches_Status] ON [Matches] ([Status]);

CREATE INDEX [IX_Matches_User1Feedback] ON [Matches] ([User1Feedback]);

CREATE INDEX [IX_Matches_User1Id] ON [Matches] ([User1Id]);

CREATE INDEX [IX_Matches_User2Feedback] ON [Matches] ([User2Feedback]);

CREATE INDEX [IX_Matches_User2Id] ON [Matches] ([User2Id]);

CREATE INDEX [IX_Participants_Email] ON [Participants] ([Email]);

CREATE INDEX [IX_QuestionOptions_QuestionId] ON [QuestionOptions] ([QuestionId]);

CREATE INDEX [IX_Registrations_Email] ON [Registrations] ([Email]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260227151333_InitialCreate', N'10.0.3');

COMMIT;
GO

