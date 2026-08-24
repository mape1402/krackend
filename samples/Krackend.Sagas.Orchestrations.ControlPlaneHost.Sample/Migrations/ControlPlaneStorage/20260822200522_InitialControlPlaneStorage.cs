using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    public partial class InitialControlPlaneStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Distribution");

            migrationBuilder.EnsureSchema(
                name: "Design");

            migrationBuilder.EnsureSchema(
                name: "Security");

            migrationBuilder.CreateTable(
                name: "Artifacts",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrchestrationVersionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrchestrationDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VersionLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VersionNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArtifactType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceEvent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BranchRuleDefinitions",
                schema: "Design",
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
                name: "Domains",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "OrchestrationVersions",
                schema: "Design",
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
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    StageDefinitionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    JoinPolicy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaxParallelAgents = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParallelGroupDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StageDefinitions",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ExecutionConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskDefinitions",
                schema: "Design",
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
                    ExecutionConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransformationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeoutPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompensationDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "Security",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriggerBindings",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationVersionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                schema: "Design",
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

            migrationBuilder.CreateTable(
                name: "Releases",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Strategy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Releases_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "Distribution",
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeNodes",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnvironmentId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    DistributionMode = table.Column<int>(type: "int", nullable: false),
                    EndpointBaseUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    EndpointApiPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AuthenticationMode = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SecretReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ApiKeyReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuntimeNodes_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalSchema: "Distribution",
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationDefinitions",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DomainId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    OwnerTeam = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OwnerTeamId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrchestrationDefinitions_Domains_DomainId",
                        column: x => x.DomainId,
                        principalSchema: "Design",
                        principalTable: "Domains",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrchestrationDefinitions_Teams_OwnerTeamId",
                        column: x => x.OwnerTeamId,
                        principalSchema: "Security",
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                schema: "Security",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    TeamId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Security",
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationAllowedRuntimeNodes",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationDefinitionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationAllowedRuntimeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrchestrationAllowedRuntimeNodes_RuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalSchema: "Distribution",
                        principalTable: "RuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReleasePlanTargets",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ReleaseId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleasePlanTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleasePlanTargets_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalSchema: "Distribution",
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReleasePlanTargets_RuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalSchema: "Distribution",
                        principalTable: "RuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseTargets",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ArtifactId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ReleaseId = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    RolloutGroup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivationStatus = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RuntimeVersionApplied = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseTargets_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "Distribution",
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReleaseTargets_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalSchema: "Distribution",
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReleaseTargets_RuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalSchema: "Distribution",
                        principalTable: "RuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeCapabilities",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeCapabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuntimeCapabilities_RuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalSchema: "Distribution",
                        principalTable: "RuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseAttempts",
                schema: "Distribution",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ReleaseTargetId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InitiatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseAttempts_ReleaseTargets_ReleaseTargetId",
                        column: x => x.ReleaseTargetId,
                        principalSchema: "Distribution",
                        principalTable: "ReleaseTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_OrchestrationVersionId_ArtifactType",
                schema: "Distribution",
                table: "Artifacts",
                columns: new[] { "OrchestrationVersionId", "ArtifactType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchRuleDefinitions_StageDefinitionId",
                schema: "Design",
                table: "BranchRuleDefinitions",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_Description",
                schema: "Design",
                table: "Domains",
                column: "Description");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_DisplayName",
                schema: "Design",
                table: "Domains",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Domains_Key",
                schema: "Design",
                table: "Domains",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Environments_Code",
                schema: "Distribution",
                table: "Environments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationAllowedRuntimeNodes_OrchestrationDefinitionId_RuntimeNodeId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes",
                columns: new[] { "OrchestrationDefinitionId", "RuntimeNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationAllowedRuntimeNodes_RuntimeNodeId",
                schema: "Distribution",
                table: "OrchestrationAllowedRuntimeNodes",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationDefinitions_DomainId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationDefinitions_Key",
                schema: "Design",
                table: "OrchestrationDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationDefinitions_OwnerTeamId",
                schema: "Design",
                table: "OrchestrationDefinitions",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId",
                schema: "Design",
                table: "OrchestrationVersions",
                column: "OrchestrationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId_Version",
                schema: "Design",
                table: "OrchestrationVersions",
                columns: new[] { "OrchestrationDefinitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParallelGroupDefinitions_StageDefinitionId",
                schema: "Design",
                table: "ParallelGroupDefinitions",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseAttempts_ReleaseTargetId",
                schema: "Distribution",
                table: "ReleaseAttempts",
                column: "ReleaseTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePlanTargets_ReleaseId_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleasePlanTargets",
                columns: new[] { "ReleaseId", "RuntimeNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePlanTargets_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleasePlanTargets",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ArtifactId",
                schema: "Distribution",
                table: "Releases",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_OrchestrationDefinitionId",
                schema: "Distribution",
                table: "Releases",
                column: "OrchestrationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTargets_ArtifactId",
                schema: "Distribution",
                table: "ReleaseTargets",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTargets_ReleaseId",
                schema: "Distribution",
                table: "ReleaseTargets",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTargets_RuntimeNodeId",
                schema: "Distribution",
                table: "ReleaseTargets",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeCapabilities_RuntimeNodeId",
                schema: "Distribution",
                table: "RuntimeCapabilities",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_Code",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeNodes_EnvironmentId",
                schema: "Distribution",
                table: "RuntimeNodes",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StageDefinitions_OrchestrationVersionId",
                schema: "Design",
                table: "StageDefinitions",
                column: "OrchestrationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StageDefinitions_OrchestrationVersionId_Order",
                schema: "Design",
                table: "StageDefinitions",
                columns: new[] { "OrchestrationVersionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDefinitions_StageDefinitionId",
                schema: "Design",
                table: "TaskDefinitions",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDefinitions_StageDefinitionId_Order",
                schema: "Design",
                table: "TaskDefinitions",
                columns: new[] { "StageDefinitionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId_ExternalUserId",
                schema: "Security",
                table: "TeamMembers",
                columns: new[] { "TeamId", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DisplayName",
                schema: "Security",
                table: "Teams",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Key",
                schema: "Security",
                table: "Teams",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriggerBindings_OrchestrationVersionId",
                schema: "Design",
                table: "TriggerBindings",
                column: "OrchestrationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_VariableDefinitions_OrchestrationVersionId",
                schema: "Design",
                table: "VariableDefinitions",
                column: "OrchestrationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_VariableDefinitions_OrchestrationVersionId_Key",
                schema: "Design",
                table: "VariableDefinitions",
                columns: new[] { "OrchestrationVersionId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchRuleDefinitions",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "OrchestrationAllowedRuntimeNodes",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "OrchestrationDefinitions",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "OrchestrationVersions",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "ParallelGroupDefinitions",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "ReleaseAttempts",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "ReleasePlanTargets",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "RuntimeCapabilities",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "StageDefinitions",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "TaskDefinitions",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "TeamMembers",
                schema: "Security");

            migrationBuilder.DropTable(
                name: "TriggerBindings",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "VariableDefinitions",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "Domains",
                schema: "Design");

            migrationBuilder.DropTable(
                name: "ReleaseTargets",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "Security");

            migrationBuilder.DropTable(
                name: "Releases",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "RuntimeNodes",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "Artifacts",
                schema: "Distribution");

            migrationBuilder.DropTable(
                name: "Environments",
                schema: "Distribution");
        }
    }
}
