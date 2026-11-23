using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class RemoveCrossSagaContention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CallbackPending",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CallbackReceiptId",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingCallbackPayload",
                schema: "Runtime",
                table: "TaskDispatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteTopic",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteVersion",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeferralReasonCode",
                schema: "Runtime",
                table: "DurableAction",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingReason",
                schema: "Runtime",
                table: "DurableAction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Existing routes remain readable through the null projection fallback.
            // Backfill valid messaging settings before enabling the selective index.
            migrationBuilder.Sql("""
                UPDATE [Runtime].[RuntimeIngressConfigurations]
                SET [RouteTopic] = UPPER(COALESCE(JSON_VALUE([SettingsPayload], '$.topic'), JSON_VALUE([SettingsPayload], '$.Topic'))),
                    [RouteVersion] = UPPER(COALESCE(JSON_VALUE([SettingsPayload], '$.version'), JSON_VALUE([SettingsPayload], '$.Version')))
                WHERE [IngressTransport] = 'Messaging' AND ISJSON([SettingsPayload]) = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches",
                columns: new[] { "CallbackPending", "CallbackAcceptedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeIngressConfigurations_RouteTopic_RouteVersion_IsActive",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations",
                columns: new[] { "RouteTopic", "RouteVersion", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_Status_PendingReason_NextAttemptOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                columns: new[] { "Status", "PendingReason", "NextAttemptOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Runtime].[TaskDispatches] WHERE [CallbackPending] = 1)
                    THROW 51000, 'Process accepted callback receipts before removing their durable payload columns.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeIngressConfigurations_RouteTopic_RouteVersion_IsActive",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_DurableAction_Status_PendingReason_NextAttemptOnUtc",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.DropColumn(
                name: "CallbackAcceptedOnUtc",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "CallbackPending",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "CallbackReceiptId",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "PendingCallbackPayload",
                schema: "Runtime",
                table: "TaskDispatches");

            migrationBuilder.DropColumn(
                name: "RouteTopic",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations");

            migrationBuilder.DropColumn(
                name: "RouteVersion",
                schema: "Runtime",
                table: "RuntimeIngressConfigurations");

            migrationBuilder.DropColumn(
                name: "DeferralReasonCode",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.DropColumn(
                name: "PendingReason",
                schema: "Runtime",
                table: "DurableAction");
        }
    }
}
