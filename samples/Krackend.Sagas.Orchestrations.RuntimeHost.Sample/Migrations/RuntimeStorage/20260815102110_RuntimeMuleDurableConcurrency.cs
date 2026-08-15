using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class RuntimeMuleDurableConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuleActions_Key_DeduplicationKey",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.DropIndex(
                name: "IX_MuleActions_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.AddColumn<string>(
                name: "Lane",
                schema: "Runtime",
                table: "MuleActions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.Sql("""
                UPDATE [Runtime].[MuleActions]
                SET [Lane] = 'default'
                WHERE [Lane] IS NULL OR LTRIM(RTRIM([Lane])) = ''
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TerminalOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Lane_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                columns: new[] { "Lane", "Status", "NextAttemptOnUtc", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_MuleActions_Key_DeduplicationKey",
                schema: "Runtime",
                table: "MuleActions",
                columns: new[] { "Key", "DeduplicationKey" },
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuleActions_Lane_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.DropIndex(
                name: "UX_MuleActions_Key_DeduplicationKey",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.DropColumn(
                name: "Lane",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.DropColumn(
                name: "StartedOnUtc",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.DropColumn(
                name: "TerminalOnUtc",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Key_DeduplicationKey",
                schema: "Runtime",
                table: "MuleActions",
                columns: new[] { "Key", "DeduplicationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleActions_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                columns: new[] { "Status", "NextAttemptOnUtc", "CreatedOnUtc" });
        }
    }
}
