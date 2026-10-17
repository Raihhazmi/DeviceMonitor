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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE TABLE [ComplianceRules] (
        [Id] int NOT NULL IDENTITY,
        [SoftwareName] nvarchar(256) NOT NULL,
        [MinimumVersion] nvarchar(64) NULL,
        [ProhibitedVersions] nvarchar(512) NULL,
        [IsMandatory] bit NOT NULL,
        [Notes] nvarchar(512) NULL,
        [IsEnabled] bit NOT NULL,
        CONSTRAINT [PK_ComplianceRules] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE TABLE [Devices] (
        [Id] int NOT NULL IDENTITY,
        [Hostname] nvarchar(128) NOT NULL,
        [AssetTag] nvarchar(64) NULL,
        [DeviceType] nvarchar(32) NOT NULL,
        [Manufacturer] nvarchar(128) NULL,
        [Model] nvarchar(128) NULL,
        [SerialNumber] nvarchar(128) NULL,
        [OsName] nvarchar(128) NULL,
        [OsVersion] nvarchar(64) NULL,
        [OsBuild] nvarchar(32) NULL,
        [OsArchitecture] nvarchar(16) NULL,
        [BiosVendor] nvarchar(128) NULL,
        [BiosVersion] nvarchar(64) NULL,
        [BiosReleaseDate] datetime2 NULL,
        [CpuName] nvarchar(128) NULL,
        [CpuCores] int NOT NULL,
        [CpuLogicalProcessors] int NOT NULL,
        [RamTotalGb] float(10) NOT NULL,
        [IpAddress] nvarchar(64) NULL,
        [MacAddress] nvarchar(32) NULL,
        [Domain] nvarchar(128) NULL,
        [Department] nvarchar(64) NULL,
        [Location] nvarchar(64) NULL,
        [AssignedUser] nvarchar(128) NULL,
        [Status] nvarchar(16) NOT NULL,
        [AgentVersion] nvarchar(32) NOT NULL,
        [LastSeenUtc] datetime2 NULL,
        [FirstSeenUtc] datetime2 NOT NULL,
        [TotalDiskGb] float(12) NOT NULL,
        [UsedDiskGb] float(12) NOT NULL,
        [UptimeSeconds] float(18) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE TABLE [AgentRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [FinishedAtUtc] datetime2 NULL,
        [DurationMs] float(12) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [AgentVersion] nvarchar(32) NOT NULL,
        [CollectorsRun] nvarchar(256) NULL,
        [ItemsSent] int NOT NULL,
        [OfflineQueueCount] int NOT NULL,
        [CpuPercent] float(6) NOT NULL,
        [MemoryMb] float(10) NOT NULL,
        [ErrorMessage] nvarchar(1024) NULL,
        [TriggerSource] nvarchar(32) NULL,
        CONSTRAINT [PK_AgentRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AgentRuns_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE TABLE [DeviceSnapshots] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [CollectedAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CpuUsagePercent] float(6) NOT NULL,
        [RamUsedGb] float(10) NOT NULL,
        [RamTotalGb] float(10) NOT NULL,
        [RamUsedPercent] float(6) NOT NULL,
        [DiskTotalGb] float(12) NOT NULL,
        [DiskUsedGb] float(12) NOT NULL,
        [DiskFreeGb] float(12) NOT NULL,
        [DiskUsedPercent] float(6) NOT NULL,
        [CFreeGb] float(12) NOT NULL,
        [UptimeSeconds] float(18) NOT NULL,
        [ProcessCount] bigint NOT NULL,
        [AgentCpuPercent] float(6) NOT NULL,
        [AgentMemoryMb] float(10) NOT NULL,
        [OfflineQueueCount] int NOT NULL,
        [NetworkRxBytes] bigint NOT NULL,
        [NetworkTxBytes] bigint NOT NULL,
        CONSTRAINT [PK_DeviceSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeviceSnapshots_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE TABLE [DiskDrives] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [DriveLetter] nvarchar(8) NOT NULL,
        [Label] nvarchar(128) NULL,
        [DriveType] nvarchar(32) NOT NULL,
        [MediaType] nvarchar(32) NOT NULL,
        [FileSystem] nvarchar(16) NULL,
        [SizeGb] float(12) NOT NULL,
        [UsedGb] float(12) NOT NULL,
        [FreeGb] float(12) NOT NULL,
        [UsedPercent] float(6) NOT NULL,
        [HealthStatus] nvarchar(16) NOT NULL,
        [CollectedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DiskDrives] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DiskDrives_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE TABLE [SoftwareInstalls] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Version] nvarchar(64) NOT NULL,
        [Publisher] nvarchar(256) NULL,
        [InstallDate] datetime2 NULL,
        [SizeMb] float(12) NOT NULL,
        [Scope] nvarchar(32) NULL,
        [ComplianceStatus] nvarchar(32) NOT NULL,
        [FirstSeenUtc] datetime2 NOT NULL,
        [LastSeenUtc] datetime2 NOT NULL,
        [IsInstalled] bit NOT NULL,
        CONSTRAINT [PK_SoftwareInstalls] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SoftwareInstalls_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AgentRuns_DeviceId_StartedAtUtc] ON [AgentRuns] ([DeviceId], [StartedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AgentRuns_StartedAtUtc] ON [AgentRuns] ([StartedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ComplianceRules_SoftwareName] ON [ComplianceRules] ([SoftwareName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DeviceSnapshots_CollectedAtUtc] ON [DeviceSnapshots] ([CollectedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DeviceSnapshots_DeviceId_CollectedAtUtc] ON [DeviceSnapshots] ([DeviceId], [CollectedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Devices_Department] ON [Devices] ([Department]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Devices_Hostname] ON [Devices] ([Hostname]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Devices_Status] ON [Devices] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DiskDrives_DeviceId_DriveLetter] ON [DiskDrives] ([DeviceId], [DriveLetter]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DiskDrives_DriveLetter] ON [DiskDrives] ([DriveLetter]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SoftwareInstalls_ComplianceStatus] ON [SoftwareInstalls] ([ComplianceStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SoftwareInstalls_DeviceId_Name_Version] ON [SoftwareInstalls] ([DeviceId], [Name], [Version]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SoftwareInstalls_Name] ON [SoftwareInstalls] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SoftwareInstalls_Publisher] ON [SoftwareInstalls] ([Publisher]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017035453_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017035453_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

