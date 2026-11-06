using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage;

/// <summary>Adds optional per-version instance lifetime settings to the SQL Server sample host.</summary>
[DbContext(typeof(ControlPlaneDbContext))]
[Migration("20261105000000_AddOrchestrationInstanceDurations")]
public sealed class AddOrchestrationInstanceDurations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "MaxInstanceDurationSeconds", schema: "Design", table: "OrchestrationVersions", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "IdleDurationSeconds", schema: "Design", table: "OrchestrationVersions", type: "int", nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MaxInstanceDurationSeconds", schema: "Design", table: "OrchestrationVersions");
        migrationBuilder.DropColumn(name: "IdleDurationSeconds", schema: "Design", table: "OrchestrationVersions");
    }
}
