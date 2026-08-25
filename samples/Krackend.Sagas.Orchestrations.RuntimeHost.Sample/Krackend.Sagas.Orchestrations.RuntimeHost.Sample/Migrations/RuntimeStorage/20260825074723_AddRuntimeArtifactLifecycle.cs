using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeArtifactLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "IngressGeneration",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectionCompletedOnUtc",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectionError",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectionFailedOnUtc",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectionStartedOnUtc",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("""
                UPDATE [Runtime].[RuntimeOrchestrationArtifacts]
                SET [IngressGeneration] = 1,
                    [Status] = CASE WHEN [IsActive] = 1 THEN N'Ready' ELSE N'Retired' END,
                    [ProjectionCompletedOnUtc] = CASE WHEN [IsActive] = 1 THEN COALESCE([ActivatedOnUtc], [DeployedOnUtc]) ELSE NULL END,
                    [ActivatedOnUtc] = CASE WHEN [IsActive] = 1 THEN COALESCE([ActivatedOnUtc], [DeployedOnUtc]) ELSE [ActivatedOnUtc] END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_Status_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts",
                columns: new[] { "EnvironmentKey", "Status", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeOrchestrationArtifacts_EnvironmentKey_Status_IsActive",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "IngressGeneration",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "ProjectionCompletedOnUtc",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "ProjectionError",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "ProjectionFailedOnUtc",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "ProjectionStartedOnUtc",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Runtime",
                table: "RuntimeOrchestrationArtifacts");
        }
    }
}
