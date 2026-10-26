using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddSagaStatisticsProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StatisticsRevision",
                schema: "Runtime",
                table: "OrchestrationInstances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "SagaStatisticsBackfillCheckpoints",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaStatisticsBackfillCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaStatisticsObservations",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InstanceId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Processed = table.Column<bool>(type: "bit", nullable: false),
                    StatusChanged = table.Column<bool>(type: "bit", nullable: false),
                    StatusFirstReached = table.Column<bool>(type: "bit", nullable: false),
                    IsBackfill = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaStatisticsObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaStatisticsProjections",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastObservedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReachedStatuses = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaStatisticsProjections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationInstances_StatisticsRevision",
                schema: "Runtime",
                table: "OrchestrationInstances",
                column: "StatisticsRevision");

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "InstanceId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_OrchestrationKey_OccurredOnUtc",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "OrchestrationKey", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsObservations_Processed_InstanceId_Revision",
                schema: "Runtime",
                table: "SagaStatisticsObservations",
                columns: new[] { "Processed", "InstanceId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsProjections_OrchestrationKey_StartedOnUtc",
                schema: "Runtime",
                table: "SagaStatisticsProjections",
                columns: new[] { "OrchestrationKey", "StartedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaStatisticsProjections_Status_StartedOnUtc",
                schema: "Runtime",
                table: "SagaStatisticsProjections",
                columns: new[] { "Status", "StartedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SagaStatisticsBackfillCheckpoints",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaStatisticsObservations",
                schema: "Runtime");

            migrationBuilder.DropTable(
                name: "SagaStatisticsProjections",
                schema: "Runtime");

            migrationBuilder.DropIndex(
                name: "IX_OrchestrationInstances_StatisticsRevision",
                schema: "Runtime",
                table: "OrchestrationInstances");

            migrationBuilder.DropColumn(
                name: "StatisticsRevision",
                schema: "Runtime",
                table: "OrchestrationInstances");
        }
    }
}
