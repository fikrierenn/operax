-- ASP.NET Core Identity Tables
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetRoles] (
        [Id]               NVARCHAR (450) NOT NULL,
        [Name]             NVARCHAR (256) NULL,
        [NormalizedName]   NVARCHAR (256) NULL,
        [ConcurrencyStamp] NVARCHAR (MAX) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName] ASC) WHERE ([NormalizedName] IS NOT NULL);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUsers')
BEGIN
    CREATE TABLE [dbo].[AspNetUsers] (
        [Id]                   NVARCHAR (450)     NOT NULL,
        [UserName]             NVARCHAR (256)     NULL,
        [NormalizedUserName]   NVARCHAR (256)     NULL,
        [Email]                NVARCHAR (256)     NULL,
        [NormalizedEmail]      NVARCHAR (256)     NULL,
        [EmailConfirmed]       BIT                NOT NULL,
        [PasswordHash]         NVARCHAR (MAX)     NULL,
        [SecurityStamp]        NVARCHAR (MAX)     NULL,
        [ConcurrencyStamp]     NVARCHAR (MAX)     NULL,
        [PhoneNumber]          NVARCHAR (MAX)     NULL,
        [PhoneNumberConfirmed] BIT                NOT NULL,
        [TwoFactorEnabled]     BIT                NOT NULL,
        [LockoutEnd]           DATETIMEOFFSET (7) NULL,
        [LockoutEnabled]       BIT                NOT NULL,
        [AccessFailedCount]    INT                NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName] ASC) WHERE ([NormalizedUserName] IS NOT NULL);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetRoleClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims] (
        [Id]         INT            IDENTITY (1, 1) NOT NULL,
        [RoleId]     NVARCHAR (450) NOT NULL,
        [ClaimType]  NVARCHAR (MAX) NULL,
        [ClaimValue] NVARCHAR (MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId] ASC);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUserClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims] (
        [Id]         INT            IDENTITY (1, 1) NOT NULL,
        [UserId]     NVARCHAR (450) NOT NULL,
        [ClaimType]  NVARCHAR (MAX) NULL,
        [ClaimValue] NVARCHAR (MAX) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId] ASC);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUserLogins')
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins] (
        [LoginProvider]       NVARCHAR (450) NOT NULL,
        [ProviderKey]         NVARCHAR (450) NOT NULL,
        [ProviderDisplayName] NVARCHAR (MAX) NULL,
        [UserId]              NVARCHAR (450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED ([LoginProvider] ASC, [ProviderKey] ASC),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId] ASC);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUserRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles] (
        [UserId] NVARCHAR (450) NOT NULL,
        [RoleId] NVARCHAR (450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId] ASC);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUserTokens')
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens] (
        [UserId]        NVARCHAR (450) NOT NULL,
        [LoginProvider] NVARCHAR (450) NOT NULL,
        [Name]          NVARCHAR (450) NOT NULL,
        [Value]         NVARCHAR (MAX) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED ([UserId] ASC, [LoginProvider] ASC, [Name] ASC),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END

-- Seed Admin User (Eğer yoksa)
IF NOT EXISTS (SELECT * FROM AspNetUsers WHERE Email = 'admin@operax.com')
BEGIN
    DECLARE @AdminId NVARCHAR(450) = NEWID();
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
    VALUES (@AdminId, 'admin', 'ADMIN', 'admin@operax.com', 'ADMIN@OPERAX.COM', 1, 'AQAAAAIAAYagAAAAEP0...', NEWID(), NEWID(), 0, 0, 1, 0);

    IF NOT EXISTS (SELECT * FROM AspNetRoles WHERE Name = 'Administrator')
    BEGIN
        DECLARE @RoleId NVARCHAR(450) = NEWID();
        INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
        VALUES (@RoleId, 'Administrator', 'ADMINISTRATOR', NEWID());

        INSERT INTO AspNetUserRoles (UserId, RoleId)
        VALUES (@AdminId, @RoleId);
    END
END
