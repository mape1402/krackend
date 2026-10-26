using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class IndexRuntimeInstanceMonitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_StartedOnUtc_Status",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "StartedOnUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Status_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "Status", "LastUpdatedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_StartedOnUtc_Status",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Status_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");
        }
    }
}
