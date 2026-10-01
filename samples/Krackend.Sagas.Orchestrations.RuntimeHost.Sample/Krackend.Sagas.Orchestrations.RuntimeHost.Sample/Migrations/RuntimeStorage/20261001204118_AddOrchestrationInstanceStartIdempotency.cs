using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddOrchestrationInstanceStartIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StartIdempotencyKey",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_StartIdempotencyKey",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "StartIdempotencyKey",
                unique: true,
                filter: "[StartIdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_StartIdempotencyKey",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "StartIdempotencyKey",
                schema: "Runtime",
                table: "OrchestrationInstances");
        }
    }
}
