using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class AddRuntimeDesignNodeLifecycleStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("""
                UPDATE [Runtime].[RuntimeDesignNodes]
                SET [Status] = CASE
                    WHEN [IsEnabled] = CAST(1 AS bit) THEN N'Enabled'
                    ELSE N'Suspend'
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeDesignNodes_Status",
                schema: "Runtime",
                table: "RuntimeDesignNodes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuntimeDesignNodes_Status",
                schema: "Runtime",
                table: "RuntimeDesignNodes");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Runtime",
                table: "RuntimeDesignNodes");
        }
    }
}
