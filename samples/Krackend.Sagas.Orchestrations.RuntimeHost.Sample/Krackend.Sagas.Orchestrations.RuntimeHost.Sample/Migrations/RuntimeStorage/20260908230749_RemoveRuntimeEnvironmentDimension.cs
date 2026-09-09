using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class RemoveRuntimeEnvironmentDimension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_Status_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_EnvironmentKey_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_EnvironmentVariableValues_EnvironmentKey_VariableKey",
                schema: "Runtime",
                table: "EnvironmentVariableValues");

            migrationBuilder.DropColumn(
                name: "EnvironmentKey",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "EnvironmentKey",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "EnvironmentKey",
                schema: "Runtime",
                table: "EnvironmentVariableValues");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "OrchestrationDefinitionKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "OrchestrationDefinitionKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_Status_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "LastUpdatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariableValues_VariableKey",
                schema: "Runtime",
                table: "EnvironmentVariableValues",
                column: "VariableKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_Status_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_EnvironmentVariableValues_VariableKey",
                schema: "Runtime",
                table: "EnvironmentVariableValues");

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentKey",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentKey",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentKey",
                schema: "Runtime",
                table: "EnvironmentVariableValues",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "EnvironmentKey", "OrchestrationDefinitionKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "EnvironmentKey", "OrchestrationDefinitionKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_Status_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "EnvironmentKey", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_EnvironmentKey_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "EnvironmentKey", "LastUpdatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariableValues_EnvironmentKey_VariableKey",
                schema: "Runtime",
                table: "EnvironmentVariableValues",
                columns: new[] { "EnvironmentKey", "VariableKey" },
                unique: true);
        }
    }
}
