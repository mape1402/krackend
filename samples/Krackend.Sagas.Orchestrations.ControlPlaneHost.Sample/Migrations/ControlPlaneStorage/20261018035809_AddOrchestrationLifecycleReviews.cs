using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    public partial class AddOrchestrationLifecycleReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "Design",
                table: "OrchestrationVersions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "DraftSlot",
                schema: "Design",
                table: "OrchestrationVersions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(
                """
                ;WITH OpenVersions AS
                (
                    SELECT [Id],
                           [OrchestrationDefinitionId],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [OrchestrationDefinitionId]
                               ORDER BY [CreatedOnUtc] DESC, [Id] DESC
                           ) AS [Ordinal]
                    FROM [Design].[OrchestrationVersions]
                    WHERE [Status] IN (N'Draft', N'PendingDeployment')
                )
                UPDATE [version]
                SET [DraftSlot] = CASE
                    WHEN [version].[Status] NOT IN (N'Draft', N'PendingDeployment')
                        THEN CONCAT(N'closed:', CONVERT(varchar(34), [version].[Id], 1))
                    WHEN [openVersion].[Ordinal] = 1
                        THEN CONCAT(N'open:', CONVERT(varchar(34), [version].[OrchestrationDefinitionId], 1))
                    ELSE CONCAT(N'legacy-open:', CONVERT(varchar(34), [version].[Id], 1))
                END
                FROM [Design].[OrchestrationVersions] AS [version]
                LEFT JOIN OpenVersions AS [openVersion] ON [openVersion].[Id] = [version].[Id];
                """);

            migrationBuilder.AlterColumn<string>(
                name: "DraftSlot",
                schema: "Design",
                table: "OrchestrationVersions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "OrchestrationVersionReviews",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PendingStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpectedChecksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApplicationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DecidedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AbortedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AbortedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AbortReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppliedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AppliedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeployedArtifactId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PendingSlot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StateToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationVersionReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersions_DraftSlot",
                schema: "Design",
                table: "OrchestrationVersions",
                column: "DraftSlot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersionReviews_OrchestrationVersionId",
                schema: "Design",
                table: "OrchestrationVersionReviews",
                column: "OrchestrationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersionReviews_PendingSlot",
                schema: "Design",
                table: "OrchestrationVersionReviews",
                column: "PendingSlot",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrchestrationVersionReviews",
                schema: "Design");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationVersions_DraftSlot",
                schema: "Design",
                table: "OrchestrationVersions");

            migrationBuilder.DropColumn(
                name: "DraftSlot",
                schema: "Design",
                table: "OrchestrationVersions");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "Design",
                table: "OrchestrationVersions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
