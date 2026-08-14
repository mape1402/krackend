using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class RuntimeMuleDurableActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MuleActions",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadType = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LockedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuleActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_DispatchStatus_ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                columns: new[] { "DispatchStatus", "ScheduledOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Key_DeduplicationKey",
                schema: "Runtime",
                table: "MuleActions",
                columns: new[] { "Key", "DeduplicationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_LockedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                column: "LockedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                columns: new[] { "Status", "NextAttemptOnUtc", "CreatedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuleActions",
                schema: "Runtime");

            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_DispatchStatus_ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "ScheduledOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");
        }
    }
}
