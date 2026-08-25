using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeDesignNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuntimeDesignNodes",
                schema: "Runtime",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EndpointBaseUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    RemoteRuntimeNodeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SecretReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProtectedSecret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastConnectionCheckedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastConnectionSucceeded = table.Column<bool>(type: "bit", nullable: true),
                    LastConnectionMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeDesignNodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_ClientId",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_IsEnabled",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_Key",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeDesignNodes",
                schema: "Runtime");
        }
    }
}
