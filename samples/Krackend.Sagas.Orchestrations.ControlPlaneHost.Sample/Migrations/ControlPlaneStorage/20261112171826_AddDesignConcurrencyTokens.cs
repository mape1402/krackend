using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    public partial class AddDesignConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                schema: "Design",
                table: "TriggerBindings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                schema: "Design",
                table: "TaskDefinitions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                schema: "Design",
                table: "StageDefinitions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                schema: "Design",
                table: "TriggerBindings");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                schema: "Design",
                table: "TaskDefinitions");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                schema: "Design",
                table: "StageDefinitions");
        }
    }
}
