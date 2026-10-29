using System;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    [DbContext(typeof(RuntimeDbContext))]
    [Migration("20261028100000_AddIdleReconciliationWindow")]
    public partial class AddIdleReconciliationWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IdleExpiresOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdleSinceUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "datetime2",
                nullable: true);

            // Waiting was an aggregate-level state in older runtime versions. Active work is now
            // represented by Running; task/attempt state carries response waiting and retries.
            migrationBuilder.Sql(
                "UPDATE [Runtime].[OrchestrationInstances] " +
                "SET [Status] = 'Running', [WaitingSinceUtc] = NULL " +
                "WHERE [Status] = 'Waiting';");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_Status_IdleExpiresOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances",
                columns: new[] { "Status", "IdleExpiresOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_Status_IdleExpiresOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "IdleExpiresOnUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "IdleSinceUtc",
                schema: "Runtime",
                table: "OrchestrationInstances");
        }
    }
}
