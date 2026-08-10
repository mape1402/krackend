using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DesignStorage
{
    /// <inheritdoc />
    public partial class DesignSchemaNamespace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Design");

            migrationBuilder.RenameTable(
                name: "VariableDefinitions",
                newName: "VariableDefinitions",
                newSchema: "Design");

            migrationBuilder.RenameTable(
                name: "TriggerBindings",
                newName: "TriggerBindings",
                newSchema: "Design");

            migrationBuilder.RenameTable(
                name: "TaskDefinitions",
                newName: "TaskDefinitions",
                newSchema: "Design");

            migrationBuilder.RenameTable(
                name: "StageDefinitions",
                newName: "StageDefinitions",
                newSchema: "Design");

            migrationBuilder.RenameTable(
                name: "ParallelGroupDefinitions",
                newName: "ParallelGroupDefinitions",
                newSchema: "Design");

            migrationBuilder.RenameTable(
                name: "OrchestrationVersions",
                newName: "OrchestrationVersions",
                newSchema: "Design");

            migrationBuilder.RenameTable(
                name: "OrchestrationDefinitions",
                newName: "OrchestrationDefinitions",
                newSchema: "Design");

            migrationBuilder.RenameTable(
                name: "BranchRuleDefinitions",
                newName: "BranchRuleDefinitions",
                newSchema: "Design");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "VariableDefinitions",
                schema: "Design",
                newName: "VariableDefinitions");

            migrationBuilder.RenameTable(
                name: "TriggerBindings",
                schema: "Design",
                newName: "TriggerBindings");

            migrationBuilder.RenameTable(
                name: "TaskDefinitions",
                schema: "Design",
                newName: "TaskDefinitions");

            migrationBuilder.RenameTable(
                name: "StageDefinitions",
                schema: "Design",
                newName: "StageDefinitions");

            migrationBuilder.RenameTable(
                name: "ParallelGroupDefinitions",
                schema: "Design",
                newName: "ParallelGroupDefinitions");

            migrationBuilder.RenameTable(
                name: "OrchestrationVersions",
                schema: "Design",
                newName: "OrchestrationVersions");

            migrationBuilder.RenameTable(
                name: "OrchestrationDefinitions",
                schema: "Design",
                newName: "OrchestrationDefinitions");

            migrationBuilder.RenameTable(
                name: "BranchRuleDefinitions",
                schema: "Design",
                newName: "BranchRuleDefinitions");
        }
    }
}
