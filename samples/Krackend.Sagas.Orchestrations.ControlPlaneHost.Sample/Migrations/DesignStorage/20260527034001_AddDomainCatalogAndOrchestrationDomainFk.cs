using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DesignStorage
{
    /// <inheritdoc />
    public partial class AddDomainCatalogAndOrchestrationDomainFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Domain",
                schema: "Design",
                table: "OrchestrationDefinitions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<byte[]>(
                name: "DomainId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Domains",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationDefinitions_DomainId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_Description",
                schema: "Design",
                table: "Domains",
                column: "Description");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_DisplayName",
                schema: "Design",
                table: "Domains",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_Key",
                schema: "Design",
                table: "Domains",
                column: "Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestrationDefinitions_Domains_DomainId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                column: "DomainId",
                principalSchema: "Design",
                principalTable: "Domains",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrchestrationDefinitions_Domains_DomainId",
                schema: "Design",
                table: "OrchestrationDefinitions");

            migrationBuilder.DropTable(
                name: "Domains",
                schema: "Design");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationDefinitions_DomainId",
                schema: "Design",
                table: "OrchestrationDefinitions");

            migrationBuilder.DropColumn(
                name: "DomainId",
                schema: "Design",
                table: "OrchestrationDefinitions");

            migrationBuilder.AlterColumn<string>(
                name: "Domain",
                schema: "Design",
                table: "OrchestrationDefinitions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }
    }
}
