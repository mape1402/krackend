using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage;

/// <summary>Adds the per-instance lifetime policy and indexed expiration deadline.</summary>
[DbContext(typeof(RuntimeDbContext))]
[Migration("20261106010000_AddInstanceExpirationProjection")]
public sealed class AddInstanceExpirationProjection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "MaxInstanceDurationSeconds", schema: "Runtime", table: "OrchestrationInstances", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "IdleDurationSeconds", schema: "Runtime", table: "OrchestrationInstances", type: "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "ExpiresOnUtc", schema: "Runtime", table: "OrchestrationInstances", type: "datetime2", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_OrchestrationInstances_Status_ExpiresOnUtc_Id", schema: "Runtime", table: "OrchestrationInstances",
            columns: ["Status", "ExpiresOnUtc", "Id"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_OrchestrationInstances_Status_ExpiresOnUtc_Id", schema: "Runtime", table: "OrchestrationInstances");
        migrationBuilder.DropColumn(name: "ExpiresOnUtc", schema: "Runtime", table: "OrchestrationInstances");
        migrationBuilder.DropColumn(name: "IdleDurationSeconds", schema: "Runtime", table: "OrchestrationInstances");
        migrationBuilder.DropColumn(name: "MaxInstanceDurationSeconds", schema: "Runtime", table: "OrchestrationInstances");
    }
}
