using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SuperAdminPrayerEntriesWaitlistCircleOtpChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredCircleId",
                table: "WaitingListEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnTimeCount",
                table: "PrayerDailyLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrayedCount",
                table: "PrayerDailyLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtpChannel",
                table: "PlatformSettings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "WhatsApp");

            migrationBuilder.AddColumn<bool>(
                name: "StudentRemindersEnabled",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "AdminPermissions",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 3); // AdminPermission.Default = ManageFinance | DeleteRecords

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PrayerEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrayerDailyLogId = table.Column<int>(type: "int", nullable: false),
                    Prayer = table.Column<int>(type: "int", nullable: false),
                    Prayed = table.Column<bool>(type: "bit", nullable: false),
                    InMosque = table.Column<bool>(type: "bit", nullable: false),
                    OnTime = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrayerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrayerEntries_PrayerDailyLogs_PrayerDailyLogId",
                        column: x => x.PrayerDailyLogId,
                        principalTable: "PrayerDailyLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_PreferredCircleId",
                table: "WaitingListEntries",
                column: "PreferredCircleId");

            migrationBuilder.CreateIndex(
                name: "IX_PrayerEntry_Log_Prayer",
                table: "PrayerEntries",
                columns: new[] { "PrayerDailyLogId", "Prayer" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WaitingListEntries_Circles_PreferredCircleId",
                table: "WaitingListEntries",
                column: "PreferredCircleId",
                principalTable: "Circles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WaitingListEntries_Circles_PreferredCircleId",
                table: "WaitingListEntries");

            migrationBuilder.DropTable(
                name: "PrayerEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitingListEntries_PreferredCircleId",
                table: "WaitingListEntries");

            migrationBuilder.DropColumn(
                name: "PreferredCircleId",
                table: "WaitingListEntries");

            migrationBuilder.DropColumn(
                name: "OnTimeCount",
                table: "PrayerDailyLogs");

            migrationBuilder.DropColumn(
                name: "PrayedCount",
                table: "PrayerDailyLogs");

            migrationBuilder.DropColumn(
                name: "OtpChannel",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "StudentRemindersEnabled",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "AdminPermissions",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "AspNetUsers");
        }
    }
}
