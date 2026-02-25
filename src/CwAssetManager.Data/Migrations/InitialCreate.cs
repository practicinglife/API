using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CwAssetManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    BiosGuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CwManageDeviceId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CwControlSessionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CwRmmDeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RequestsPerMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    BurstCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RequestBody = table.Column<string>(type: "TEXT", nullable: true),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComplianceScore = table.Column<double>(type: "REAL", nullable: false),
                    HealthScore = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetEvaluations_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_Machines_Hostname", table: "Machines", column: "Hostname");
            migrationBuilder.CreateIndex(name: "IX_Machines_MacAddress", table: "Machines", column: "MacAddress");
            migrationBuilder.CreateIndex(name: "IX_Machines_SerialNumber", table: "Machines", column: "SerialNumber");
            migrationBuilder.CreateIndex(name: "IX_RequestLogs_Provider", table: "RequestLogs", column: "Provider");
            migrationBuilder.CreateIndex(name: "IX_RequestLogs_Timestamp", table: "RequestLogs", column: "Timestamp");
            migrationBuilder.CreateIndex(name: "IX_AssetEvaluations_MachineId", table: "AssetEvaluations", column: "MachineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AssetEvaluations");
            migrationBuilder.DropTable(name: "RequestLogs");
            migrationBuilder.DropTable(name: "ProviderConfigs");
            migrationBuilder.DropTable(name: "Machines");
        }
    }
}
