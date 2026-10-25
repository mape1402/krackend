using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class IndexDispatchCancellationIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "DispatchCancellationPending", "LastUpdatedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");
        }
    }
}
