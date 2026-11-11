using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddFailureEventScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrchestrationKey",
                schema: "Runtime",
                table: "SagaFailureEvents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrchestrationVersion",
                schema: "Runtime",
                table: "SagaFailureEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeProjectionVersion",
                schema: "Runtime",
                table: "SagaFailureEvents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SagaFailureEvents_OrchestrationKey_OrchestrationVersion_TransitionType_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents",
                columns: new[] { "OrchestrationKey", "OrchestrationVersion", "TransitionType", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaFailureEvents_ScopeProjectionVersion_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents",
                columns: new[] { "ScopeProjectionVersion", "OccurredOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SagaFailureEvents_OrchestrationKey_OrchestrationVersion_TransitionType_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents");

            migrationBuilder.DropIndex(
                name: "IX_SagaFailureEvents_ScopeProjectionVersion_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents");

            migrationBuilder.DropColumn(
                name: "OrchestrationKey",
                schema: "Runtime",
                table: "SagaFailureEvents");

            migrationBuilder.DropColumn(
                name: "OrchestrationVersion",
                schema: "Runtime",
                table: "SagaFailureEvents");

            migrationBuilder.DropColumn(
                name: "ScopeProjectionVersion",
                schema: "Runtime",
                table: "SagaFailureEvents");
        }
    }
}
