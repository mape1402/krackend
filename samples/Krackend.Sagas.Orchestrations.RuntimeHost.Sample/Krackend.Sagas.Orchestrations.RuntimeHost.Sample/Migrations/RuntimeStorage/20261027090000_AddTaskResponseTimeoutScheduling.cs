using System;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    [DbContext(typeof(RuntimeDbContext))]
    [Migration("20261027090000_AddTaskResponseTimeoutScheduling")]
    public partial class AddTaskResponseTimeoutScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDeadlineUtc",
                schema: "Runtime",
                table: "TaskExecutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeoutActionId",
                schema: "Runtime",
                table: "TaskExecutions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeoutActionLane",
                schema: "Runtime",
                table: "TaskExecutions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Status_ResponseDeadlineUtc",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "Status", "ResponseDeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Status_TimeoutActionId",
                schema: "Runtime",
                table: "TaskExecutions",
                columns: new[] { "Status", "TimeoutActionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_Status_ResponseDeadlineUtc",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropIndex(
                name: "IX_TaskExecutions_Status_TimeoutActionId",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "ResponseDeadlineUtc",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "TimeoutActionId",
                schema: "Runtime",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "TimeoutActionLane",
                schema: "Runtime",
                table: "TaskExecutions");
        }
    }
}
