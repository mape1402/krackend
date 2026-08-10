using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DistributionStorage
{
    /// <inheritdoc />
    public partial class InitialDistributionStorageSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistributionArtifactReleases",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    OrchestrationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrchestrationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArtifactType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ManifestHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionArtifactReleases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistributionRuntimeNodes",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_DistributionRuntimeNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistributionPromotions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ArtifactReleaseId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Strategy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionPromotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionPromotions_DistributionArtifactReleases_ArtifactReleaseId",
                        column: x => x.ArtifactReleaseId,
                        principalTable: "DistributionArtifactReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributionRuntimeCapabilities",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionRuntimeCapabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionRuntimeCapabilities_DistributionRuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalTable: "DistributionRuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistributionAssignments",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    ArtifactReleaseId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    PromotionId = table.Column<byte[]>(type: "binary(16)", nullable: true),
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
                    table.PrimaryKey("PK_DistributionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionAssignments_DistributionArtifactReleases_ArtifactReleaseId",
                        column: x => x.ArtifactReleaseId,
                        principalTable: "DistributionArtifactReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DistributionAssignments_DistributionPromotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "DistributionPromotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DistributionAssignments_DistributionRuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalTable: "DistributionRuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributionPromotionTargets",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    PromotionId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    RuntimeNodeId = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionPromotionTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionPromotionTargets_DistributionPromotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "DistributionPromotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionPromotionTargets_DistributionRuntimeNodes_RuntimeNodeId",
                        column: x => x.RuntimeNodeId,
                        principalTable: "DistributionRuntimeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributionAttempts",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    DistributionAssignmentId = table.Column<byte[]>(type: "binary(16)", nullable: false),
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
                    table.PrimaryKey("PK_DistributionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionAttempts_DistributionAssignments_DistributionAssignmentId",
                        column: x => x.DistributionAssignmentId,
                        principalTable: "DistributionAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionArtifactReleases_OrchestrationId_OrchestrationVersion_ArtifactType",
                table: "DistributionArtifactReleases",
                columns: new[] { "OrchestrationId", "OrchestrationVersion", "ArtifactType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistributionAssignments_ArtifactReleaseId",
                table: "DistributionAssignments",
                column: "ArtifactReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionAssignments_PromotionId",
                table: "DistributionAssignments",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionAssignments_RuntimeNodeId",
                table: "DistributionAssignments",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionAttempts_DistributionAssignmentId",
                table: "DistributionAttempts",
                column: "DistributionAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionPromotions_ArtifactReleaseId",
                table: "DistributionPromotions",
                column: "ArtifactReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionPromotionTargets_PromotionId_RuntimeNodeId",
                table: "DistributionPromotionTargets",
                columns: new[] { "PromotionId", "RuntimeNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistributionPromotionTargets_RuntimeNodeId",
                table: "DistributionPromotionTargets",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionRuntimeCapabilities_RuntimeNodeId",
                table: "DistributionRuntimeCapabilities",
                column: "RuntimeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionRuntimeNodes_Code",
                table: "DistributionRuntimeNodes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributionAttempts");

            migrationBuilder.DropTable(
                name: "DistributionPromotionTargets");

            migrationBuilder.DropTable(
                name: "DistributionRuntimeCapabilities");

            migrationBuilder.DropTable(
                name: "DistributionAssignments");

            migrationBuilder.DropTable(
                name: "DistributionPromotions");

            migrationBuilder.DropTable(
                name: "DistributionRuntimeNodes");

            migrationBuilder.DropTable(
                name: "DistributionArtifactReleases");
        }
    }
}
