using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DistributionStorage
{
    /// <inheritdoc />
    public partial class DistributionEnvironmentsAndRuntimeNodesSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Environments",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_Code",
                schema: "Distribution",
                table: "Environments",
                column: "Code",
                unique: true);

            migrationBuilder.Sql(
                """
                WITH DistinctEnvironmentNames AS (
                    SELECT DISTINCT [Environment] AS [Name]
                    FROM [Distribution].[RuntimeNodes]
                    WHERE [Environment] IS NOT NULL AND LTRIM(RTRIM([Environment])) <> ''
                ),
                Numbered AS (
                    SELECT
                        [Name],
                        ROW_NUMBER() OVER (ORDER BY [Name]) AS [N]
                    FROM DistinctEnvironmentNames
                )
                INSERT INTO [Distribution].[Environments] ([Id], [Name], [Code], [Description], [IsEnabled], [CreatedAtUtc], [UpdatedAtUtc])
                SELECT
                    CAST(NEWID() AS binary(16)),
                    [Name],
                    CONCAT('ENV_', [N]),
                    '',
                    1,
                    SYSUTCDATETIME(),
                    NULL
                FROM Numbered;
                """);

            migrationBuilder.Sql(
                """
                UPDATE rn
                SET rn.[EnvironmentId] = e.[Id]
                FROM [Distribution].[RuntimeNodes] rn
                INNER JOIN [Distribution].[Environments] e
                    ON rn.[Environment] = e.[Name];
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Distribution].[RuntimeNodes] WHERE [EnvironmentId] IS NULL)
                BEGIN
                    DECLARE @DefaultEnvironmentId binary(16) = CAST(NEWID() AS binary(16));
                    INSERT INTO [Distribution].[Environments] ([Id], [Name], [Code], [Description], [IsEnabled], [CreatedAtUtc], [UpdatedAtUtc])
                    VALUES (@DefaultEnvironmentId, 'Default', 'ENV_DEFAULT', '', 1, SYSUTCDATETIME(), NULL);
                    UPDATE [Distribution].[RuntimeNodes] SET [EnvironmentId] = @DefaultEnvironmentId WHERE [EnvironmentId] IS NULL;
                END
                """);

            migrationBuilder.AlterColumn<byte[]>(
                name: "EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "binary(16)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "binary(16)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RuntimeNodes_Environments_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "EnvironmentId",
                principalSchema: "Distribution",
                principalTable: "Environments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Environment",
                schema: "Distribution",
                table: "RuntimeNodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RuntimeNodes_Environments_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropTable(
                name: "Environments",
                schema: "Distribution");

            migrationBuilder.DropIndex(
                name: "IX_RuntimeNodes_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.DropColumn(
                name: "EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes");

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                schema: "Distribution",
                table: "RuntimeNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}

