using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeIngressConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuntimeIngressConfigurations",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ConfigurationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IngressKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IngressTransport = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SettingsPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeactivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeIngressConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_IsActive_IngressTransport_IngressKind",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "IsActive", "IngressTransport", "IngressKind" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_IsActive_RuntimeOrchestrationArtifactId",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "IsActive", "RuntimeOrchestrationArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_RuntimeOrchestrationArtifactId_ConfigurationKey",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "RuntimeOrchestrationArtifactId", "ConfigurationKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeIngressConfigurations",
                schema: "Runtime");
        }
    }
}
