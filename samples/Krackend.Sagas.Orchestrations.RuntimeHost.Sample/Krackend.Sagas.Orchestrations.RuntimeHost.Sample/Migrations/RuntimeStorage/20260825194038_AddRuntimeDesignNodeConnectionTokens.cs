using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeDesignNodeConnectionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeDesignNodes_ClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "ClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "ProtectedSecret",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "SecretReference",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.AddColumn<int>(
                name: "AccessTokenTtlSeconds",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DistributionMode",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InboundAllowedScopes",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundCredentialCreatedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundCredentialRevokedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundCredentialRotatedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundCredentialStatus",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InboundKeyId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundLastFailureReason",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundLastTokenFailedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundLastTokenIssuedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundSecretHash",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutboundCredentialImportedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundCredentialStatus",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OutboundKeyId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutboundLastTokenReceivedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundRequestedScopes",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedOutboundSecret",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenRefreshSkewSeconds",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TokenValidationCacheTtlSeconds",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_InboundClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "InboundClientId",
                unique: true,
                filter: "[InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeDesignNodes_InboundClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "AccessTokenTtlSeconds",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "DistributionMode",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundAllowedScopes",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialCreatedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialRevokedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialRotatedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialStatus",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundKeyId",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundLastFailureReason",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundLastTokenFailedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundLastTokenIssuedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "InboundSecretHash",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "OutboundClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "OutboundCredentialImportedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "OutboundCredentialStatus",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "OutboundKeyId",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "OutboundLastTokenReceivedAtUtc",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "OutboundRequestedScopes",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "ProtectedOutboundSecret",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "TokenRefreshSkewSeconds",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "TokenValidationCacheTtlSeconds",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedSecret",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecretReference",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_ClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "ClientId",
                unique: true);
        }
    }
}
