using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.DesignStorage
{
    /// <inheritdoc />
    public partial class RemoveStageDefaultPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultRetryPolicyJson",
                table: "StageDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultTimeoutPolicyJson",
                table: "StageDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultRetryPolicyJson",
                table: "StageDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTimeoutPolicyJson",
                table: "StageDefinitions",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
