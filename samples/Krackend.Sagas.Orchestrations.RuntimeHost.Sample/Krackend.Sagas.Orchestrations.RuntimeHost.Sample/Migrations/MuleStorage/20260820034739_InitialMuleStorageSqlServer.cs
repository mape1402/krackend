using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.MuleStorage
{
    /// <inheritdoc />
    public partial class InitialMuleStorageSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MuleActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                    StartedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TerminalOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuleActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Lane_Status_NextAttemptOnUtc_CreatedOnUtc",
                table: "MuleActions",
                columns: new[] { "Lane", "Status", "NextAttemptOnUtc", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Status_CompletedOnUtc",
                table: "MuleActions",
                columns: new[] { "Status", "CompletedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Status_LockedOnUtc",
                table: "MuleActions",
                columns: new[] { "Status", "LockedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_MuleActions_Key_DeduplicationKey",
                table: "MuleActions",
                columns: new[] { "Key", "DeduplicationKey" },
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuleActions");
        }
    }
}
