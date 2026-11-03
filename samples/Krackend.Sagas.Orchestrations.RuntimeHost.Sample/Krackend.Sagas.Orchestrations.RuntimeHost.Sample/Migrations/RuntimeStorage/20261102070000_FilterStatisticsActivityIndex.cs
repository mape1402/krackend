using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage;

[DbContext(typeof(RuntimeDbContext))]
[Migration("20261102070000_FilterStatisticsActivityIndex")]
public sealed class FilterStatisticsActivityIndex : Migration
{
    private const string Table = "SagaStatisticsObservations";
    private const string Schema = "Runtime";
    private const string Existing = "IX_SagaStatisticsObservations_OrchestrationKey_OccurredOnUtc";
    private const string Temporary = "IX_SagaStatisticsObservations_ActivityReplacement";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(name: Temporary, schema: Schema, table: Table,
                columns: new[] { "OrchestrationKey", "OccurredOnUtc" },
                filter: "[Processed] = 1 AND [StatusChanged] = 1 AND [IsBackfill] = 0")
            .Annotation("SqlServer:Include", new[] { "Status" });
        migrationBuilder.DropIndex(name: Existing, schema: Schema, table: Table);
        migrationBuilder.Sql($"EXEC sp_rename N'[{Schema}].[{Table}].[{Temporary}]', N'{Existing}', N'INDEX';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(name: Temporary, schema: Schema, table: Table,
            columns: new[] { "OrchestrationKey", "OccurredOnUtc" });
        migrationBuilder.DropIndex(name: Existing, schema: Schema, table: Table);
        migrationBuilder.Sql($"EXEC sp_rename N'[{Schema}].[{Table}].[{Temporary}]', N'{Existing}', N'INDEX';");
    }
}
