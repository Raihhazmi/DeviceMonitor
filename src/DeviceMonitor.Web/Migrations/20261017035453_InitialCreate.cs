using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceMonitor.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoftwareName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MinimumVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProhibitedVersions = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Hostname = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AssetTag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OsName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OsBuild = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OsArchitecture = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    BiosVendor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BiosVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BiosReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CpuName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CpuCores = table.Column<int>(type: "int", nullable: false),
                    CpuLogicalProcessors = table.Column<int>(type: "int", nullable: false),
                    RamTotalGb = table.Column<double>(type: "float(10)", precision: 10, scale: 2, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AssignedUser = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AgentVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalDiskGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    UsedDiskGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    UptimeSeconds = table.Column<double>(type: "float(18)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AgentVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CollectorsRun = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ItemsSent = table.Column<int>(type: "int", nullable: false),
                    OfflineQueueCount = table.Column<int>(type: "int", nullable: false),
                    CpuPercent = table.Column<double>(type: "float(6)", precision: 6, scale: 2, nullable: false),
                    MemoryMb = table.Column<double>(type: "float(10)", precision: 10, scale: 2, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TriggerSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuns_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "float(6)", precision: 6, scale: 2, nullable: false),
                    RamUsedGb = table.Column<double>(type: "float(10)", precision: 10, scale: 2, nullable: false),
                    RamTotalGb = table.Column<double>(type: "float(10)", precision: 10, scale: 2, nullable: false),
                    RamUsedPercent = table.Column<double>(type: "float(6)", precision: 6, scale: 2, nullable: false),
                    DiskTotalGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    DiskUsedGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    DiskFreeGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    DiskUsedPercent = table.Column<double>(type: "float(6)", precision: 6, scale: 2, nullable: false),
                    CFreeGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    UptimeSeconds = table.Column<double>(type: "float(18)", precision: 18, scale: 2, nullable: false),
                    ProcessCount = table.Column<long>(type: "bigint", nullable: false),
                    AgentCpuPercent = table.Column<double>(type: "float(6)", precision: 6, scale: 2, nullable: false),
                    AgentMemoryMb = table.Column<double>(type: "float(10)", precision: 10, scale: 2, nullable: false),
                    OfflineQueueCount = table.Column<int>(type: "int", nullable: false),
                    NetworkRxBytes = table.Column<long>(type: "bigint", nullable: false),
                    NetworkTxBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceSnapshots_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiskDrives",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DriveLetter = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DriveType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileSystem = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SizeGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    UsedGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    FreeGb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    UsedPercent = table.Column<double>(type: "float(6)", precision: 6, scale: 2, nullable: false),
                    HealthStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiskDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiskDrives_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareInstalls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Publisher = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InstallDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SizeMb = table.Column<double>(type: "float(12)", precision: 12, scale: 2, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ComplianceStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsInstalled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareInstalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareInstalls_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_DeviceId_StartedAtUtc",
                table: "AgentRuns",
                columns: new[] { "DeviceId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_StartedAtUtc",
                table: "AgentRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceRules_SoftwareName",
                table: "ComplianceRules",
                column: "SoftwareName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSnapshots_CollectedAtUtc",
                table: "DeviceSnapshots",
                column: "CollectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSnapshots_DeviceId_CollectedAtUtc",
                table: "DeviceSnapshots",
                columns: new[] { "DeviceId", "CollectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Department",
                table: "Devices",
                column: "Department");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Hostname",
                table: "Devices",
                column: "Hostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Status",
                table: "Devices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DiskDrives_DeviceId_DriveLetter",
                table: "DiskDrives",
                columns: new[] { "DeviceId", "DriveLetter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiskDrives_DriveLetter",
                table: "DiskDrives",
                column: "DriveLetter");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareInstalls_ComplianceStatus",
                table: "SoftwareInstalls",
                column: "ComplianceStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareInstalls_DeviceId_Name_Version",
                table: "SoftwareInstalls",
                columns: new[] { "DeviceId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareInstalls_Name",
                table: "SoftwareInstalls",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareInstalls_Publisher",
                table: "SoftwareInstalls",
                column: "Publisher");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "ComplianceRules");

            migrationBuilder.DropTable(
                name: "DeviceSnapshots");

            migrationBuilder.DropTable(
                name: "DiskDrives");

            migrationBuilder.DropTable(
                name: "SoftwareInstalls");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
