using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class UpgradeMuleToV22Cancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhysicalCancellationAttemptCount",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhysicalCancellationCompletedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhysicalCancellationLastAttemptOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledOnUtc",
                schema: "Runtime",
                table: "MuleReplay",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches",
                column: "PhysicalCancellationPending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "PhysicalCancellationAttemptCount",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "PhysicalCancellationCompletedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "PhysicalCancellationLastAttemptOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "PhysicalCancellationPending",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "CancelledOnUtc",
                schema: "Runtime",
                table: "MuleReplay");

            migrationBuilder.DropColumn(
                name: "CancelledOnUtc",
                schema: "Runtime",
                table: "DurableAction");
        }
    }
}
