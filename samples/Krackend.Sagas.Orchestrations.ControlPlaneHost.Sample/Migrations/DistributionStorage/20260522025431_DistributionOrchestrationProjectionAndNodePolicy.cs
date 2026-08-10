using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DistributionStorage
{
    /// <inheritdoc />
    public partial class DistributionOrchestrationProjectionAndNodePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrchestrationDefinitionId",
                table: "DistributionReleases",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OrchestrationProjections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationProjections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationProjectionAllowedRuntimeNodes",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationProjectionAllowedRuntimeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrchestrationProjectionAllowedRuntimeNodes_OrchestrationProjections_OrchestrationDefinitionId",
                        column: x => x.OrchestrationDefinitionId,
                        principalTable: "OrchestrationProjections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrchestrationProjectionAllowedRuntimeNodes_DistributionRuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalTable: "DistributionRuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionReleases_OrchestrationDefinitionId",
                table: "DistributionReleases",
                column: "OrchestrationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationProjectionAllowedRuntimeNodes_OrchestrationDefinitionId_RuntimeNodeId",
                table: "OrchestrationProjectionAllowedRuntimeNodes",
                columns: new[] { "OrchestrationDefinitionId", "RuntimeNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationProjectionAllowedRuntimeNodes_RuntimeNodeId",
                table: "OrchestrationProjectionAllowedRuntimeNodes",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationProjections_Key",
                table: "OrchestrationProjections",
                column: "Key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrchestrationProjectionAllowedRuntimeNodes");

            migrationBuilder.DropTable(
                name: "OrchestrationProjections");

            migrationBuilder.DropIndex(
                name: "IX_DistributionReleases_OrchestrationDefinitionId",
                table: "DistributionReleases");

            migrationBuilder.DropColumn(
                name: "OrchestrationDefinitionId",
                table: "DistributionReleases");
        }
    }
}

