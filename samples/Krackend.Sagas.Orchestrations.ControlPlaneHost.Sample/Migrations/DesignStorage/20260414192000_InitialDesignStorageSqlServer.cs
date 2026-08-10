using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DesignStorage
{
    /// <inheritdoc />
    public partial class InitialDesignStorageSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchRuleDefinitions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StageDefinitionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    FromType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FromId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    NavigateToType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NavigateToId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchRuleDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationDefinitions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    OwnerTeam = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationVersions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VersionLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApprovedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParallelGroupDefinitions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StageDefinitionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    JoinPolicy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaxParallelAgents = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParallelGroupDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StageDefinitions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    DefaultOnErrorPolicy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultRetryPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultTimeoutPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutionConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskDefinitions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StageDefinitionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExecutionMode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParallelGroupId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    OnErrorPolicy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DispatchType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CompensationDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutionConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeoutPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransformationJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriggerBindings",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TriggerChannelJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerBindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VariableDefinitions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariableDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchRuleDefinitions_StageDefinitionId",
                table: "BranchRuleDefinitions",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationDefinitions_Key",
                table: "OrchestrationDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId",
                table: "OrchestrationVersions",
                column: "OrchestrationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId_Version",
                table: "OrchestrationVersions",
                columns: new[] { "OrchestrationDefinitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParallelGroupDefinitions_StageDefinitionId",
                table: "ParallelGroupDefinitions",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_StageDefinitions_OrchestrationVersionId",
                table: "StageDefinitions",
                column: "OrchestrationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StageDefinitions_OrchestrationVersionId_Order",
                table: "StageDefinitions",
                columns: new[] { "OrchestrationVersionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDefinitions_StageDefinitionId",
                table: "TaskDefinitions",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDefinitions_StageDefinitionId_Order",
                table: "TaskDefinitions",
                columns: new[] { "StageDefinitionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerBindings_OrchestrationVersionId",
                table: "TriggerBindings",
                column: "OrchestrationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_VariableDefinitions_OrchestrationVersionId",
                table: "VariableDefinitions",
                column: "OrchestrationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_VariableDefinitions_OrchestrationVersionId_Key",
                table: "VariableDefinitions",
                columns: new[] { "OrchestrationVersionId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchRuleDefinitions");

            migrationBuilder.DropTable(
                name: "OrchestrationDefinitions");

            migrationBuilder.DropTable(
                name: "OrchestrationVersions");

            migrationBuilder.DropTable(
                name: "ParallelGroupDefinitions");

            migrationBuilder.DropTable(
                name: "StageDefinitions");

            migrationBuilder.DropTable(
                name: "TaskDefinitions");

            migrationBuilder.DropTable(
                name: "TriggerBindings");

            migrationBuilder.DropTable(
                name: "VariableDefinitions");
        }
    }
}
