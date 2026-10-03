using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeExtensionPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuntimeExtensionPackages",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    BundleId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExtensionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Manifest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeExtensionPackages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeExtensionPackages_BundleId_ExtensionKey_Version",
                schema: "Runtime",
                table: "RuntimeExtensionPackages",
                columns: new[] { "BundleId", "ExtensionKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeExtensionPackages_ExtensionKey_Version_Status",
                schema: "Runtime",
                table: "RuntimeExtensionPackages",
                columns: new[] { "ExtensionKey", "Version", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeExtensionPackages",
                schema: "Runtime");
        }
    }
}
