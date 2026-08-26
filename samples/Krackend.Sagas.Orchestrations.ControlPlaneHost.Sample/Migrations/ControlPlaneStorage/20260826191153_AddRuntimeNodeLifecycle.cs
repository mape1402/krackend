using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeNodeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_Code",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.AddColumn<string>(
                name: "StatusText",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("""
                UPDATE [Distribution].[RuntimeNodes]
                SET [StatusText] = CASE [Status]
                    WHEN 1 THEN N'Enabled'
                    WHEN 2 THEN N'Suspend'
                    WHEN 3 THEN N'Suspend'
                    ELSE N'Pending'
                END,
                [IsEnabled] = CASE [Status]
                    WHEN 1 THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END
                """);

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.RenameColumn(
                name: "StatusText",
                schema: "Distribution",
                table: "RuntimeNodes",
                newName: "Status");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_Code",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "InboundClientId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_IsDeleted",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_Code",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_IsDeleted",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.AddColumn<int>(
                name: "StatusValue",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE [Distribution].[RuntimeNodes]
                SET [StatusValue] = CASE [Status]
                    WHEN N'Enabled' THEN 1
                    WHEN N'Suspend' THEN 2
                    WHEN N'Pending' THEN 3
                    ELSE 3
                END,
                [IsEnabled] = CASE [Status]
                    WHEN N'Enabled' THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END
                """);

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.RenameColumn(
                name: "StatusValue",
                schema: "Distribution",
                table: "RuntimeNodes",
                newName: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_Code",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_InboundClientId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "InboundClientId",
                unique: true,
                filter: "[InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
        }
    }
}
