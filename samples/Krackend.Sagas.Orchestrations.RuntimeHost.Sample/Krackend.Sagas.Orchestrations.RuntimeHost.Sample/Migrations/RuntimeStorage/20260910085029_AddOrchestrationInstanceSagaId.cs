using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddOrchestrationInstanceSagaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SagaId",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [Runtime].[OrchestrationInstances] SET [SagaId] = [CorrelationId] WHERE [SagaId] = '' OR [SagaId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_SagaId",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "SagaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_SagaId",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "SagaId",
                schema: "Runtime",
                table: "OrchestrationInstances");
        }
    }
}
