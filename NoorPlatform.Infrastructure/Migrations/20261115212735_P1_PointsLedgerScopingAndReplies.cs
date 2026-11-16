using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P1_PointsLedgerScopingAndReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendance_StudentId_Date",
                table: "Attendances");

            migrationBuilder.AddColumn<int>(
                name: "RecipientUserId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsAwarded",
                table: "HifzRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsAwarded",
                table: "Attendances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeacherId",
                table: "Announcements",
                type: "int",
                nullable: true);

            // ─── تنظيف سجلات الحضور المكررة قبل إنشاء القيد الفريد: يبقى أحدث سجل (أكبر Id) لكل (طالب، يوم، حلقة) ───
            migrationBuilder.Sql(@"
WITH d AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY StudentId, [Date], CircleId ORDER BY Id DESC) AS rn
    FROM Attendances
)
DELETE FROM Attendances WHERE Id IN (SELECT Id FROM d WHERE rn > 1);");

            // ─── تعبئة النقاط الممنوحة للسجلات السابقة بما كان يُمنح فعلاً وقت إنشائها،
            // حتى يسحب الحذف/التعديل اللاحق النقاط الصحيحة دون أن يضاعفها ───
            migrationBuilder.Sql(@"
DECLARE @att INT = COALESCE((SELECT TOP 1 PointsAttendancePresent FROM PlatformSettings), 10);
DECLARE @mem INT = COALESCE((SELECT TOP 1 PointsHifzMemorization FROM PlatformSettings), 50);
DECLARE @rev INT = COALESCE((SELECT TOP 1 PointsHifzRevision FROM PlatformSettings), 20);
UPDATE Attendances SET PointsAwarded = @att WHERE Status = 0;          -- Present
UPDATE HifzRecords SET PointsAwarded = CASE WHEN [Type] = 0 THEN @mem ELSE @rev END;");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RecipientUserId",
                table: "Messages",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Attendance_Student_Date_Circle",
                table: "Attendances",
                columns: new[] { "StudentId", "Date", "CircleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TeacherId",
                table: "Announcements",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Teachers_TeacherId",
                table: "Announcements",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_RecipientUserId",
                table: "Messages",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Teachers_TeacherId",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_RecipientUserId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_RecipientUserId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "UX_Attendance_Student_Date_Circle",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_TeacherId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "PointsAwarded",
                table: "HifzRecords");

            migrationBuilder.DropColumn(
                name: "PointsAwarded",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "Announcements");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_StudentId_Date",
                table: "Attendances",
                columns: new[] { "StudentId", "Date" });
        }
    }
}
