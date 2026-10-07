using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    [Migration("20261006120000_AddCompensationExecutionDeduplication")]
    public partial class AddCompensationExecutionDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CompensationExecutions_Instance_SourceTask_CompensationTask",
                schema: "Runtime",
                table: "CompensationExecutions",
                columns: new[] { "OrchestrationInstanceId", "SourceTaskExecutionId", "CompensationTaskKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompensationExecutions_Instance_SourceTask_CompensationTask",
                schema: "Runtime",
                table: "CompensationExecutions");
        }
    }
}
