using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class RuntimeArtifactType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Artifacts_EnvironmentKey_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "Artifacts");

            migrationBuilder.AddColumn<string>(
                name: "ArtifactType",
                schema: "Runtime",
                table: "Artifacts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.Sql("""
                UPDATE [Runtime].[Artifacts]
                SET [ArtifactType] =
                    CASE
                        WHEN [Notes] LIKE 'ArtifactType=%;%' THEN
                            SUBSTRING(
                                [Notes],
                                LEN('ArtifactType=') + 1,
                                CHARINDEX(';', [Notes]) - LEN('ArtifactType=') - 1)
                        WHEN [Notes] LIKE 'ArtifactType=%' THEN
                            SUBSTRING([Notes], LEN('ArtifactType=') + 1, LEN([Notes]))
                        ELSE 'unknown'
                    END
                WHERE [ArtifactType] = 'unknown';
                """);

            migrationBuilder.Sql("""
                UPDATE [Runtime].[Artifacts]
                SET
                    [IsActive] = 0,
                    [ActivatedOnUtc] = NULL,
                    [RetiredOnUtc] = COALESCE([RetiredOnUtc], SYSUTCDATETIME())
                WHERE [ArtifactType] <> 'orchestration.deploy'
                    AND [IsActive] = 1;

                WITH [RankedDeploys] AS
                (
                    SELECT
                        [Id],
                        ROW_NUMBER() OVER (
                            PARTITION BY [EnvironmentKey], [OrchestrationDefinitionKey]
                            ORDER BY [DeployedOnUtc] DESC) AS [DeployRank]
                    FROM [Runtime].[Artifacts]
                    WHERE [ArtifactType] = 'orchestration.deploy'
                )
                UPDATE [Artifact]
                SET
                    [IsActive] = CASE WHEN [RankedDeploys].[DeployRank] = 1 THEN 1 ELSE 0 END,
                    [ActivatedOnUtc] = CASE
                        WHEN [RankedDeploys].[DeployRank] = 1 THEN COALESCE([Artifact].[ActivatedOnUtc], [Artifact].[DeployedOnUtc])
                        ELSE [Artifact].[ActivatedOnUtc]
                    END,
                    [RetiredOnUtc] = CASE
                        WHEN [RankedDeploys].[DeployRank] = 1 THEN NULL
                        ELSE COALESCE([Artifact].[RetiredOnUtc], SYSUTCDATETIME())
                    END
                FROM [Runtime].[Artifacts] AS [Artifact]
                INNER JOIN [RankedDeploys]
                    ON [Artifact].[Id] = [RankedDeploys].[Id];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_EnvironmentKey_OrchestrationDefinitionKey_Version_ArtifactType",
                schema: "Runtime",
                table: "Artifacts",
                columns: new[] { "EnvironmentKey", "OrchestrationDefinitionKey", "Version", "ArtifactType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Artifacts_EnvironmentKey_OrchestrationDefinitionKey_Version_ArtifactType",
                schema: "Runtime",
                table: "Artifacts");

            migrationBuilder.DropColumn(
                name: "ArtifactType",
                schema: "Runtime",
                table: "Artifacts");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_EnvironmentKey_OrchestrationDefinitionKey_Version",
                schema: "Runtime",
                table: "Artifacts",
                columns: new[] { "EnvironmentKey", "OrchestrationDefinitionKey", "Version" },
                unique: true);
        }
    }
}
