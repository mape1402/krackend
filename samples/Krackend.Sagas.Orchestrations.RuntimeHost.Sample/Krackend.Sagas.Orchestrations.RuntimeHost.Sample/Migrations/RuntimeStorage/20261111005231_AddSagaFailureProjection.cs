using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddSagaFailureProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SagaFailureBackfillCheckpoints",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BeforeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CursorUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CursorOffset = table.Column<int>(type: "int", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaFailureBackfillCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaFailureEvents",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationInstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransitionType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaFailureEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SagaFailureEvents_TransitionType_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaFailureEvents",
                columns: new[] { "TransitionType", "OccurredOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SagaFailureBackfillCheckpoints",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaFailureEvents",
                schema: "Runtime");
        }
    }
}
