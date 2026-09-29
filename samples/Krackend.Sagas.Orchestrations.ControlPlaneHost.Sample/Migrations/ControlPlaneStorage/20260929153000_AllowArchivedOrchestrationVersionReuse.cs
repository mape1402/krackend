using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    [DbContext(typeof(ControlPlaneDbContext))]
    [Migration("20260929153000_AllowArchivedOrchestrationVersionReuse")]
    public partial class AllowArchivedOrchestrationVersionReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId_Version",
                schema: "Design",
                table: "OrchestrationVersions");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId_Version",
                schema: "Design",
                table: "OrchestrationVersions",
                columns: new[] { "OrchestrationDefinitionId", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId_Version",
                schema: "Design",
                table: "OrchestrationVersions");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationVersions_OrchestrationDefinitionId_Version",
                schema: "Design",
                table: "OrchestrationVersions",
                columns: new[] { "OrchestrationDefinitionId", "Version" },
                unique: true);
        }
    }
}
