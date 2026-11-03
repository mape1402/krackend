using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage;

/// <summary>Adds nullable progress projections; historical instances require a verified backfill.</summary>
[DbContext(typeof(RuntimeDbContext))]
[Migration("20261101010000_AddInstanceProgressProjection")]
public sealed class AddInstanceProgressProjection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "OrchestrationVersion", schema: "Runtime",
            table: "OrchestrationInstances", type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<int>(name: "TotalTaskCount", schema: "Runtime",
            table: "OrchestrationInstances", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "CompletedTaskCount", schema: "Runtime",
            table: "OrchestrationInstances", type: "int", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OrchestrationVersion", schema: "Runtime", table: "OrchestrationInstances");
        migrationBuilder.DropColumn(name: "TotalTaskCount", schema: "Runtime", table: "OrchestrationInstances");
        migrationBuilder.DropColumn(name: "CompletedTaskCount", schema: "Runtime", table: "OrchestrationInstances");
    }
}
