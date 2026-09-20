using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    public partial class RestoreDistributionEnvironmentDimension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultEnvironmentId = new byte[] { 0x01, 0x98, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xA0 };

            migrationBuilder.AlterColumn<string>(
                name: "Tags",
                schema: "Design",
                table: "OrchestrationDefinitions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Environments",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "Distribution",
                table: "Environments",
                columns: new[] { "Id", "Name", "Code", "Description", "IsEnabled", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[] { defaultEnvironmentId, "Dev", "dev", "Local development environment seeded for demos.", true, new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.AddColumn<byte[]>(
                name: "EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "binary(16)",
                nullable: false,
                defaultValue: defaultEnvironmentId);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_Code",
                schema: "Distribution",
                table: "Environments",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RuntimeNodes_Environments_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "EnvironmentId",
                principalSchema: "Distribution",
                principalTable: "Environments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RuntimeNodes_Environments_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropTable(
                name: "Environments",
                schema: "Distribution");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.AlterColumn<string>(
                name: "Tags",
                schema: "Design",
                table: "OrchestrationDefinitions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
