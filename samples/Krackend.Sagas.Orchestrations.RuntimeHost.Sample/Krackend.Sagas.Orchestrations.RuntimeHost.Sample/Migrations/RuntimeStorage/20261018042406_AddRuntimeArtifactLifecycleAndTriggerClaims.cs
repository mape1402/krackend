using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeArtifactLifecycleAndTriggerClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastControlPlaneArtifactId",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastReleaseId",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "ReleaseStrategy",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageDisposition",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RuntimeArtifactDeploymentHistory",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrchestrationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ControlPlaneArtifactId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReleaseId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ArtifactType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReleaseStrategy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RuntimeStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AppliedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeArtifactDeploymentHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeTriggerClaims",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    RuntimeOrchestrationArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeTriggerClaims", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_LifecycleStatus_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "LifecycleStatus", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeArtifactDeploymentHistory_ControlPlaneArtifactId",
                schema: "Runtime",
                table: "RuntimeArtifactDeploymentHistory",
                column: "ControlPlaneArtifactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeArtifactDeploymentHistory_OrchestrationDefinitionKey_AppliedOnUtc",
                schema: "Runtime",
                table: "RuntimeArtifactDeploymentHistory",
                columns: new[] { "OrchestrationDefinitionKey", "AppliedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeArtifactDeploymentHistory_ReleaseId",
                schema: "Runtime",
                table: "RuntimeArtifactDeploymentHistory",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeTriggerClaims_RuntimeOrchestrationArtifactId",
                schema: "Runtime",
                table: "RuntimeTriggerClaims",
                column: "RuntimeOrchestrationArtifactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeArtifactDeploymentHistory",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "RuntimeTriggerClaims",
                schema: "Runtime");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_LifecycleStatus_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "LastControlPlaneArtifactId",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "LastReleaseId",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "ReleaseStrategy",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "MessageDisposition",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations");
        }
    }
}
