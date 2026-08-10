using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DesignStorage
{
    /// <inheritdoc />
    public partial class AddOwnerTeamProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "OwnerTeamId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamProjections",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamProjections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationDefinitions_OwnerTeamId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamProjections_DisplayName",
                schema: "Design",
                table: "TeamProjections",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_TeamProjections_Key",
                schema: "Design",
                table: "TeamProjections",
                column: "Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestrationDefinitions_TeamProjections_OwnerTeamId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                column: "OwnerTeamId",
                principalSchema: "Design",
                principalTable: "TeamProjections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrchestrationDefinitions_TeamProjections_OwnerTeamId",
                schema: "Design",
                table: "OrchestrationDefinitions");

            migrationBuilder.DropTable(
                name: "TeamProjections",
                schema: "Design");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationDefinitions_OwnerTeamId",
                schema: "Design",
                table: "OrchestrationDefinitions");

            migrationBuilder.DropColumn(
                name: "OwnerTeamId",
                schema: "Design",
                table: "OrchestrationDefinitions");
        }
    }
}
