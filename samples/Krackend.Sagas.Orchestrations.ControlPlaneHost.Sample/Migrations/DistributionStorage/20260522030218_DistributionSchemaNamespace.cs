using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DistributionStorage
{
    /// <inheritdoc />
    public partial class DistributionSchemaNamespace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrchestrationProjectionAllowedRuntimeNodes_OrchestrationProjections_OrchestrationDefinitionId",
                table: "OrchestrationProjectionAllowedRuntimeNodes");

            migrationBuilder.DropForeignKey(
                name: "FK_OrchestrationProjectionAllowedRuntimeNodes_DistributionRuntimeNodes_RuntimeNodeId",
                table: "OrchestrationProjectionAllowedRuntimeNodes");

            migrationBuilder.DropForeignKey(
                name: "FK_DistributionReleases_DistributionArtifacts_ArtifactId",
                table: "DistributionReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_DistributionReleaseTargets_DistributionReleases_ReleaseId",
                table: "DistributionReleaseTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_DistributionReleaseTargets_DistributionRuntimeNodes_RuntimeNodeId",
                table: "DistributionReleaseTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_DistributionRuntimeCapabilities_DistributionRuntimeNodes_RuntimeNodeId",
                table: "DistributionRuntimeCapabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseTargets_DistributionArtifacts_ArtifactId",
                table: "ReleaseTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseTargets_DistributionReleases_ReleaseId",
                table: "ReleaseTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseTargets_DistributionRuntimeNodes_RuntimeNodeId",
                table: "ReleaseTargets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DistributionRuntimeNodes",
                table: "DistributionRuntimeNodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DistributionRuntimeCapabilities",
                table: "DistributionRuntimeCapabilities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DistributionReleaseTargets",
                table: "DistributionReleaseTargets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DistributionReleases",
                table: "DistributionReleases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrchestrationProjections",
                table: "OrchestrationProjections");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrchestrationProjectionAllowedRuntimeNodes",
                table: "OrchestrationProjectionAllowedRuntimeNodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DistributionArtifacts",
                table: "DistributionArtifacts");

            migrationBuilder.EnsureSchema(
                name: "Distribution");

            migrationBuilder.Sql("ALTER SCHEMA [Distribution] TRANSFER [dbo].[ReleaseTargets];");
            migrationBuilder.Sql("ALTER SCHEMA [Distribution] TRANSFER [dbo].[ReleaseAttempts];");

            migrationBuilder.RenameTable(
                name: "DistributionRuntimeNodes",
                newName: "RuntimeNodes",
                newSchema: "Distribution");

            migrationBuilder.RenameTable(
                name: "DistributionRuntimeCapabilities",
                newName: "RuntimeCapabilities",
                newSchema: "Distribution");

            migrationBuilder.RenameTable(
                name: "DistributionReleaseTargets",
                newName: "ReleasePlanTargets",
                newSchema: "Distribution");

            migrationBuilder.RenameTable(
                name: "DistributionReleases",
                newName: "Releases",
                newSchema: "Distribution");

            migrationBuilder.RenameTable(
                name: "OrchestrationProjections",
                newName: "Orchestrations",
                newSchema: "Distribution");

            migrationBuilder.RenameTable(
                name: "OrchestrationProjectionAllowedRuntimeNodes",
                newName: "OrchestrationAllowedRuntimeNodes",
                newSchema: "Distribution");

            migrationBuilder.RenameTable(
                name: "DistributionArtifacts",
                newName: "Artifacts",
                newSchema: "Distribution");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionRuntimeNodes_Code",
                schema: "Distribution",
                table: "RuntimeNodes",
                newName: "IX_RuntimeNodes_Code");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionRuntimeCapabilities_RuntimeNodeId",
                schema: "Distribution",
                table: "RuntimeCapabilities",
                newName: "IX_RuntimeCapabilities_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionReleaseTargets_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleasePlanTargets",
                newName: "IX_ReleasePlanTargets_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionReleaseTargets_ReleaseId_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleasePlanTargets",
                newName: "IX_ReleasePlanTargets_ReleaseId_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionReleases_OrchestrationDefinitionId",
                schema: "Distribution",
                table: "Releases",
                newName: "IX_Releases_OrchestrationDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionReleases_ArtifactId",
                schema: "Distribution",
                table: "Releases",
                newName: "IX_Releases_ArtifactId");

            migrationBuilder.RenameIndex(
                name: "IX_OrchestrationProjections_Key",
                schema: "Distribution",
                table: "Orchestrations",
                newName: "IX_Orchestrations_Key");

            migrationBuilder.RenameIndex(
                name: "IX_OrchestrationProjectionAllowedRuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes",
                newName: "IX_OrchestrationAllowedRuntimeNodes_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_OrchestrationProjectionAllowedRuntimeNodes_OrchestrationDefinitionId_RuntimeNodeId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes",
                newName: "IX_OrchestrationAllowedRuntimeNodes_OrchestrationDefinitionId_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionArtifacts_OrchestrationVersionId_ArtifactType",
                schema: "Distribution",
                table: "Artifacts",
                newName: "IX_Artifacts_OrchestrationVersionId_ArtifactType");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RuntimeNodes",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RuntimeCapabilities",
                schema: "Distribution",
                table: "RuntimeCapabilities",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReleasePlanTargets",
                schema: "Distribution",
                table: "ReleasePlanTargets",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Releases",
                schema: "Distribution",
                table: "Releases",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Orchestrations",
                schema: "Distribution",
                table: "Orchestrations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrchestrationAllowedRuntimeNodes",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Artifacts",
                schema: "Distribution",
                table: "Artifacts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestrationAllowedRuntimeNodes_Orchestrations_OrchestrationDefinitionId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes",
                column: "OrchestrationDefinitionId",
                principalSchema: "Distribution",
                principalTable: "Orchestrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestrationAllowedRuntimeNodes_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes",
                column: "RuntimeNodeId",
                principalSchema: "Distribution",
                principalTable: "RuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleasePlanTargets_Releases_ReleaseId",
                schema: "Distribution",
                table: "ReleasePlanTargets",
                column: "ReleaseId",
                principalSchema: "Distribution",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleasePlanTargets_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleasePlanTargets",
                column: "RuntimeNodeId",
                principalSchema: "Distribution",
                principalTable: "RuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Releases_Artifacts_ArtifactId",
                schema: "Distribution",
                table: "Releases",
                column: "ArtifactId",
                principalSchema: "Distribution",
                principalTable: "Artifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseTargets_Artifacts_ArtifactId",
                schema: "Distribution",
                table: "ReleaseTargets",
                column: "ArtifactId",
                principalSchema: "Distribution",
                principalTable: "Artifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseTargets_Releases_ReleaseId",
                schema: "Distribution",
                table: "ReleaseTargets",
                column: "ReleaseId",
                principalSchema: "Distribution",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseTargets_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleaseTargets",
                column: "RuntimeNodeId",
                principalSchema: "Distribution",
                principalTable: "RuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RuntimeCapabilities_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "RuntimeCapabilities",
                column: "RuntimeNodeId",
                principalSchema: "Distribution",
                principalTable: "RuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrchestrationAllowedRuntimeNodes_Orchestrations_OrchestrationDefinitionId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes");

            migrationBuilder.DropForeignKey(
                name: "FK_OrchestrationAllowedRuntimeNodes_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleasePlanTargets_Releases_ReleaseId",
                schema: "Distribution",
                table: "ReleasePlanTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleasePlanTargets_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleasePlanTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_Releases_Artifacts_ArtifactId",
                schema: "Distribution",
                table: "Releases");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseTargets_Artifacts_ArtifactId",
                schema: "Distribution",
                table: "ReleaseTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseTargets_Releases_ReleaseId",
                schema: "Distribution",
                table: "ReleaseTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseTargets_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleaseTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_RuntimeCapabilities_RuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "RuntimeCapabilities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RuntimeNodes",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RuntimeCapabilities",
                schema: "Distribution",
                table: "RuntimeCapabilities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Releases",
                schema: "Distribution",
                table: "Releases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReleasePlanTargets",
                schema: "Distribution",
                table: "ReleasePlanTargets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Orchestrations",
                schema: "Distribution",
                table: "Orchestrations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrchestrationAllowedRuntimeNodes",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Artifacts",
                schema: "Distribution",
                table: "Artifacts");

            migrationBuilder.Sql("ALTER SCHEMA [dbo] TRANSFER [Distribution].[ReleaseTargets];");
            migrationBuilder.Sql("ALTER SCHEMA [dbo] TRANSFER [Distribution].[ReleaseAttempts];");

            migrationBuilder.RenameTable(
                name: "RuntimeNodes",
                schema: "Distribution",
                newName: "DistributionRuntimeNodes");

            migrationBuilder.RenameTable(
                name: "RuntimeCapabilities",
                schema: "Distribution",
                newName: "DistributionRuntimeCapabilities");

            migrationBuilder.RenameTable(
                name: "Releases",
                schema: "Distribution",
                newName: "DistributionReleases");

            migrationBuilder.RenameTable(
                name: "ReleasePlanTargets",
                schema: "Distribution",
                newName: "DistributionReleaseTargets");

            migrationBuilder.RenameTable(
                name: "Orchestrations",
                schema: "Distribution",
                newName: "OrchestrationProjections");

            migrationBuilder.RenameTable(
                name: "OrchestrationAllowedRuntimeNodes",
                schema: "Distribution",
                newName: "OrchestrationProjectionAllowedRuntimeNodes");

            migrationBuilder.RenameTable(
                name: "Artifacts",
                schema: "Distribution",
                newName: "DistributionArtifacts");

            migrationBuilder.RenameIndex(
                name: "IX_RuntimeNodes_Code",
                table: "DistributionRuntimeNodes",
                newName: "IX_DistributionRuntimeNodes_Code");

            migrationBuilder.RenameIndex(
                name: "IX_RuntimeCapabilities_RuntimeNodeId",
                table: "DistributionRuntimeCapabilities",
                newName: "IX_DistributionRuntimeCapabilities_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Releases_OrchestrationDefinitionId",
                table: "DistributionReleases",
                newName: "IX_DistributionReleases_OrchestrationDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_Releases_ArtifactId",
                table: "DistributionReleases",
                newName: "IX_DistributionReleases_ArtifactId");

            migrationBuilder.RenameIndex(
                name: "IX_ReleasePlanTargets_RuntimeNodeId",
                table: "DistributionReleaseTargets",
                newName: "IX_DistributionReleaseTargets_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_ReleasePlanTargets_ReleaseId_RuntimeNodeId",
                table: "DistributionReleaseTargets",
                newName: "IX_DistributionReleaseTargets_ReleaseId_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Orchestrations_Key",
                table: "OrchestrationProjections",
                newName: "IX_OrchestrationProjections_Key");

            migrationBuilder.RenameIndex(
                name: "IX_OrchestrationAllowedRuntimeNodes_RuntimeNodeId",
                table: "OrchestrationProjectionAllowedRuntimeNodes",
                newName: "IX_OrchestrationProjectionAllowedRuntimeNodes_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_OrchestrationAllowedRuntimeNodes_OrchestrationDefinitionId_RuntimeNodeId",
                table: "OrchestrationProjectionAllowedRuntimeNodes",
                newName: "IX_OrchestrationProjectionAllowedRuntimeNodes_OrchestrationDefinitionId_RuntimeNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Artifacts_OrchestrationVersionId_ArtifactType",
                table: "DistributionArtifacts",
                newName: "IX_DistributionArtifacts_OrchestrationVersionId_ArtifactType");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DistributionRuntimeNodes",
                table: "DistributionRuntimeNodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DistributionRuntimeCapabilities",
                table: "DistributionRuntimeCapabilities",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DistributionReleases",
                table: "DistributionReleases",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DistributionReleaseTargets",
                table: "DistributionReleaseTargets",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrchestrationProjections",
                table: "OrchestrationProjections",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrchestrationProjectionAllowedRuntimeNodes",
                table: "OrchestrationProjectionAllowedRuntimeNodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DistributionArtifacts",
                table: "DistributionArtifacts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestrationProjectionAllowedRuntimeNodes_OrchestrationProjections_OrchestrationDefinitionId",
                table: "OrchestrationProjectionAllowedRuntimeNodes",
                column: "OrchestrationDefinitionId",
                principalTable: "OrchestrationProjections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrchestrationProjectionAllowedRuntimeNodes_DistributionRuntimeNodes_RuntimeNodeId",
                table: "OrchestrationProjectionAllowedRuntimeNodes",
                column: "RuntimeNodeId",
                principalTable: "DistributionRuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionReleases_DistributionArtifacts_ArtifactId",
                table: "DistributionReleases",
                column: "ArtifactId",
                principalTable: "DistributionArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionReleaseTargets_DistributionReleases_ReleaseId",
                table: "DistributionReleaseTargets",
                column: "ReleaseId",
                principalTable: "DistributionReleases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionReleaseTargets_DistributionRuntimeNodes_RuntimeNodeId",
                table: "DistributionReleaseTargets",
                column: "RuntimeNodeId",
                principalTable: "DistributionRuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionRuntimeCapabilities_DistributionRuntimeNodes_RuntimeNodeId",
                table: "DistributionRuntimeCapabilities",
                column: "RuntimeNodeId",
                principalTable: "DistributionRuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseTargets_DistributionArtifacts_ArtifactId",
                table: "ReleaseTargets",
                column: "ArtifactId",
                principalTable: "DistributionArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseTargets_DistributionReleases_ReleaseId",
                table: "ReleaseTargets",
                column: "ReleaseId",
                principalTable: "DistributionReleases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseTargets_DistributionRuntimeNodes_RuntimeNodeId",
                table: "ReleaseTargets",
                column: "RuntimeNodeId",
                principalTable: "DistributionRuntimeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

