using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    [DbContext(typeof(ControlPlaneDbContext))]
    [Migration("20260929053000_AddMetadataDescriptorSourceKey")]
    public partial class AddMetadataDescriptorSourceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                schema: "Design",
                table: "MetadataDescriptors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataDescriptors_SourceKey",
                schema: "Design",
                table: "MetadataDescriptors",
                column: "SourceKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataDescriptors_SourceKey",
                schema: "Design",
                table: "MetadataDescriptors");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                schema: "Design",
                table: "MetadataDescriptors");
        }
    }
}
