using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Migrations.ControlPlaneStorage
{
    /// <inheritdoc />
    public partial class AddMetadataDescriptors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetadataDescriptors",
                schema: "Design",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataDescriptors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataDescriptors_ContentHash",
                schema: "Design",
                table: "MetadataDescriptors",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataDescriptors_DisplayName",
                schema: "Design",
                table: "MetadataDescriptors",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataDescriptors_Key",
                schema: "Design",
                table: "MetadataDescriptors",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetadataDescriptors",
                schema: "Design");
        }
    }
}
