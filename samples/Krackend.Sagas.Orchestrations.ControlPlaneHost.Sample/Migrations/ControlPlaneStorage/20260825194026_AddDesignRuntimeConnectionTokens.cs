using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    public partial class AddDesignRuntimeConnectionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecretReference",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "ClientId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "AuthenticationMode",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "ApiKeyReference",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.AddColumn<int>(
                name: "AccessTokenTtlSeconds",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InboundAllowedScopes",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundKeyId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundCredentialCreatedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundCredentialRevokedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundCredentialRotatedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundCredentialStatus",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InboundLastFailureReason",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundLastTokenFailedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundLastTokenIssuedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundSecretHash",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutboundCredentialImportedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundCredentialStatus",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OutboundKeyId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutboundLastTokenReceivedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutboundRequestedScopes",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedOutboundSecret",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenValidationCacheTtlSeconds",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TokenRefreshSkewSeconds",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "InboundClientId",
                unique: true,
                filter: "[InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "AccessTokenTtlSeconds",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundAllowedScopes",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundKeyId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialCreatedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialRevokedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialRotatedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundCredentialStatus",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundLastFailureReason",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundLastTokenFailedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundLastTokenIssuedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "InboundSecretHash",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "OutboundCredentialImportedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "OutboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "OutboundCredentialStatus",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "OutboundKeyId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "OutboundLastTokenReceivedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "OutboundRequestedScopes",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "ProtectedOutboundSecret",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "TokenRefreshSkewSeconds",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "TokenValidationCacheTtlSeconds",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.AddColumn<int>(
                name: "AuthenticationMode",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecretReference",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiKeyReference",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }
    }
}
