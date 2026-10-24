using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Migrations.RuntimeStorage
{
    /// <inheritdoc />
    public partial class UpgradeMuleToV21Replay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MuleActions",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.DropIndex(
                name: "UX_MuleActions_Key_DeduplicationKey",
                schema: "Runtime",
                table: "MuleActions");

            migrationBuilder.RenameTable(
                name: "MuleActions",
                schema: "Runtime",
                newName: "DurableAction",
                newSchema: "Runtime");

            migrationBuilder.RenameIndex(
                name: "IX_MuleActions_Status_LockedOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                newName: "IX_DurableAction_Status_LockedOnUtc");

            migrationBuilder.RenameIndex(
                name: "IX_MuleActions_Status_CompletedOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                newName: "IX_DurableAction_Status_CompletedOnUtc");

            migrationBuilder.RenameIndex(
                name: "IX_MuleActions_Lane_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "DurableAction",
                newName: "IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc");

            migrationBuilder.AddColumn<string>(
                name: "MuleV2Id",
                schema: "Runtime",
                table: "DurableAction",
                type: "nvarchar(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MuleV2ConcurrencyToken",
                schema: "Runtime",
                table: "DurableAction",
                type: "nvarchar(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.Sql(
                @"EXEC(N'
                CREATE FUNCTION [Runtime].[ToMuleV2Ulid]
                (
                    @legacyId uniqueidentifier,
                    @createdOnUtc datetimeoffset
                )
                RETURNS varchar(26)
                AS
                BEGIN
                    DECLARE @alphabet varchar(32) = ''0123456789ABCDEFGHJKMNPQRSTVWXYZ'';
                    DECLARE @timestamp bigint = DATEDIFF_BIG(
                        millisecond,
                        CONVERT(datetimeoffset, ''1970-01-01T00:00:00+00:00''),
                        SWITCHOFFSET(@createdOnUtc, ''+00:00''));
                    DECLARE @timestampPart varchar(10) = '''';
                    DECLARE @randomnessPart varchar(16) = '''';
                    DECLARE @position int = 10;
                    DECLARE @bytes varbinary(16) = CONVERT(varbinary(16), @legacyId);
                    DECLARE @bytePosition int = 1;
                    DECLARE @buffer bigint = 0;
                    DECLARE @bits int = 0;

                    IF @timestamp < 0
                        SET @timestamp = 0;

                    WHILE @position > 0
                    BEGIN
                        SET @timestampPart = SUBSTRING(@alphabet, (@timestamp % 32) + 1, 1) + @timestampPart;
                        SET @timestamp = @timestamp / 32;
                        SET @position = @position - 1;
                    END;

                    WHILE @bytePosition <= 16
                    BEGIN
                        SET @buffer = (@buffer * 256) + CONVERT(int, SUBSTRING(@bytes, @bytePosition, 1));
                        SET @bits = @bits + 8;

                        WHILE @bits >= 5
                        BEGIN
                            SET @bits = @bits - 5;
                            SET @randomnessPart = @randomnessPart + SUBSTRING(
                                @alphabet,
                                ((@buffer / POWER(CAST(2 AS bigint), @bits)) % 32) + 1,
                                1);
                            SET @buffer = @buffer % POWER(CAST(2 AS bigint), @bits);
                        END;

                        SET @bytePosition = @bytePosition + 1;
                    END;

                    RETURN @timestampPart + @randomnessPart;
                END;
                ')");

            migrationBuilder.Sql(
                @"
                UPDATE [Runtime].[DurableAction]
                SET [MuleV2Id] = [Runtime].[ToMuleV2Ulid]([Id], [CreatedOnUtc]),
                    [MuleV2ConcurrencyToken] = [Runtime].[ToMuleV2Ulid](NEWID(), [CreatedOnUtc]);
                ");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.RenameColumn(
                name: "MuleV2Id",
                schema: "Runtime",
                table: "DurableAction",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "MuleV2ConcurrencyToken",
                schema: "Runtime",
                table: "DurableAction",
                newName: "ConcurrencyToken");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "Runtime",
                table: "DurableAction",
                type: "nvarchar(26)",
                maxLength: 26,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(26)",
                oldMaxLength: 26,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyToken",
                schema: "Runtime",
                table: "DurableAction",
                type: "nvarchar(26)",
                maxLength: 26,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(26)",
                oldMaxLength: 26,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DurableAction",
                schema: "Runtime",
                table: "DurableAction",
                column: "Id");

            migrationBuilder.Sql("DROP FUNCTION [Runtime].[ToMuleV2Ulid];");

            migrationBuilder.CreateTable(
                name: "MuleReplay",
                schema: "Runtime",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    SourceActionId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ReplayRootActionId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ReplayActionId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ReplaySequence = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Lane = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OriginalDeduplicationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduleOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LockedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockToken = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    MaterializedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuleReplay", x => x.RequestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DurableAction_Key_DeduplicationKey",
                schema: "Runtime",
                table: "DurableAction",
                columns: new[] { "Key", "DeduplicationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleReplay_ReplayActionId",
                schema: "Runtime",
                table: "MuleReplay",
                column: "ReplayActionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuleReplay_SourceActionId_BranchId",
                schema: "Runtime",
                table: "MuleReplay",
                columns: new[] { "SourceActionId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuleReplay_Status_LockedOnUtc",
                schema: "Runtime",
                table: "MuleReplay",
                columns: new[] { "Status", "LockedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MuleReplay_Status_ScheduleOnUtc_NextAttemptOnUtc",
                schema: "Runtime",
                table: "MuleReplay",
                columns: new[] { "Status", "ScheduleOnUtc", "NextAttemptOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuleReplay",
                schema: "Runtime");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DurableAction",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.DropIndex(
                name: "IX_DurableAction_Key_DeduplicationKey",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.Sql(
                @"EXEC(N'
                CREATE FUNCTION [Runtime].[FromMuleV2Ulid]
                (
                    @muleId varchar(26)
                )
                RETURNS uniqueidentifier
                AS
                BEGIN
                    DECLARE @alphabet varchar(32) = ''0123456789ABCDEFGHJKMNPQRSTVWXYZ'';
                    DECLARE @bytes varbinary(16) = 0x;
                    DECLARE @position int = 11;
                    DECLARE @buffer bigint = 0;
                    DECLARE @bits int = 0;
                    DECLARE @value int;

                    IF @muleId IS NULL OR LEN(@muleId) <> 26
                        RETURN NULL;

                    WHILE @position <= 26
                    BEGIN
                        SET @value = CHARINDEX(SUBSTRING(@muleId, @position, 1), @alphabet) - 1;
                        IF @value < 0
                            RETURN NULL;

                        SET @buffer = (@buffer * 32) + @value;
                        SET @bits = @bits + 5;

                        WHILE @bits >= 8
                        BEGIN
                            SET @bits = @bits - 8;
                            SET @bytes = @bytes + CONVERT(binary(1), (@buffer / POWER(CAST(2 AS bigint), @bits)) % 256);
                            SET @buffer = @buffer % POWER(CAST(2 AS bigint), @bits);
                        END;

                        SET @position = @position + 1;
                    END;

                    RETURN CONVERT(uniqueidentifier, @bytes);
                END;
                ')");

            migrationBuilder.AddColumn<Guid>(
                name: "MuleV1Id",
                schema: "Runtime",
                table: "DurableAction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                @"
                UPDATE [Runtime].[DurableAction]
                SET [MuleV1Id] = [Runtime].[FromMuleV2Ulid]([Id]);
                ");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "Runtime",
                table: "DurableAction");

            migrationBuilder.RenameColumn(
                name: "MuleV1Id",
                schema: "Runtime",
                table: "DurableAction",
                newName: "Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "Runtime",
                table: "DurableAction",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.Sql("DROP FUNCTION [Runtime].[FromMuleV2Ulid];");

            migrationBuilder.RenameTable(
                name: "DurableAction",
                schema: "Runtime",
                newName: "MuleActions",
                newSchema: "Runtime");

            migrationBuilder.RenameIndex(
                name: "IX_DurableAction_Status_LockedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                newName: "IX_MuleActions_Status_LockedOnUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DurableAction_Status_CompletedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                newName: "IX_MuleActions_Status_CompletedOnUtc");

            migrationBuilder.RenameIndex(
                name: "IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc",
                schema: "Runtime",
                table: "MuleActions",
                newName: "IX_MuleActions_Lane_Status_NextAttemptOnUtc_CreatedOnUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MuleActions",
                schema: "Runtime",
                table: "MuleActions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "UX_MuleActions_Key_DeduplicationKey",
                schema: "Runtime",
                table: "MuleActions",
                columns: new[] { "Key", "DeduplicationKey" },
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");
        }
    }
}
