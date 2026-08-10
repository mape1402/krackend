using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DistributionStorage
{
    /// <inheritdoc />
    public partial class DistributionArtifactSelfContained : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DistributionArtifacts_OrchestrationId_OrchestrationVersion_ArtifactType",
                table: "DistributionArtifacts");

            migrationBuilder.RenameColumn(
                name: "OrchestrationVersion",
                table: "DistributionArtifacts",
                newName: "VersionNumber");

            migrationBuilder.RenameColumn(
                name: "OrchestrationId",
                table: "DistributionArtifacts",
                newName: "OrchestrationVersionId");

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationDefinitionId",
                table: "DistributionArtifacts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationDisplayName",
                table: "DistributionArtifacts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VersionLabel",
                table: "DistributionArtifacts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistributionArtifacts_OrchestrationVersionId_ArtifactType",
                table: "DistributionArtifacts",
                columns: new[] { "OrchestrationVersionId", "ArtifactType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DistributionArtifacts_OrchestrationVersionId_ArtifactType",
                table: "DistributionArtifacts");

            migrationBuilder.DropColumn(
                name: "OrchestrationDefinitionId",
                table: "DistributionArtifacts");

            migrationBuilder.DropColumn(
                name: "OrchestrationDisplayName",
                table: "DistributionArtifacts");

            migrationBuilder.DropColumn(
                name: "VersionLabel",
                table: "DistributionArtifacts");

            migrationBuilder.RenameColumn(
                name: "VersionNumber",
                table: "DistributionArtifacts",
                newName: "OrchestrationVersion");

            migrationBuilder.RenameColumn(
                name: "OrchestrationVersionId",
                table: "DistributionArtifacts",
                newName: "OrchestrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionArtifacts_OrchestrationId_OrchestrationVersion_ArtifactType",
                table: "DistributionArtifacts",
                columns: new[] { "OrchestrationId", "OrchestrationVersion", "ArtifactType" },
                unique: true);
        }
    }
}
