using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddLastExecutionCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastExecutedStageKey",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastExecutedTaskKey",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastExecutedStageKey",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "LastExecutedTaskKey",
                schema: "Runtime",
                table: "OrchestrationInstances");
        }
    }
}
