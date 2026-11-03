using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage;

[DbContext(typeof(RuntimeDbContext))]
[Migration("20261102060000_FilterStatisticsBackfillIndex")]
public sealed class FilterStatisticsBackfillIndex : Migration
{
    private const string Table = "OrchestrationInstances";
    private const string Schema = "Runtime";
    private const string Existing = "IX_OrchestrationInstances_StatisticsRevision";
    private const string Temporary = "IX_OrchestrationInstances_StatisticsRevisionReplacement";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve an index for the backfill throughout the transactional swap.
        migrationBuilder.CreateIndex(name: Temporary, schema: Schema, table: Table,
            column: "StatisticsRevision", filter: "[StatisticsRevision] = 0");
        migrationBuilder.DropIndex(name: Existing, schema: Schema, table: Table);
        migrationBuilder.Sql($"EXEC sp_rename N'[{Schema}].[{Table}].[{Temporary}]', N'{Existing}', N'INDEX';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(name: Temporary, schema: Schema, table: Table,
            column: "StatisticsRevision");
        migrationBuilder.DropIndex(name: Existing, schema: Schema, table: Table);
        migrationBuilder.Sql($"EXEC sp_rename N'[{Schema}].[{Table}].[{Temporary}]', N'{Existing}', N'INDEX';");
    }
}
