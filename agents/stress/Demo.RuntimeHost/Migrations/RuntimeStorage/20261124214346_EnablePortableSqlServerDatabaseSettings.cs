using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;

namespace Demo.RuntimeHost.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class EnablePortableSqlServerDatabaseSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.ApplyKrackendSqlServerPortableTuning();
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Portable database and index tuning is intentionally not reverted.
        }
    }
}
