using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GradeScaleAttendancePointsAndToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableDress",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableMatn",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableParentHome",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePrayer",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsAttendanceExcused",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsAttendanceLate",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "PointsAttendanceUnexcused",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsHifzMemGood",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "PointsHifzMemVeryGood",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 40);

            migrationBuilder.AddColumn<int>(
                name: "PointsHifzRevAcceptable",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "PointsHifzRevGood",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "PointsHifzRevVeryGood",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "PointsMatnMemorization",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "PointsAwarded",
                table: "MatnRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // سلّم التقديرات الموحّد: «ضعيف» لم يعد تقديراً ناجحاً (الحقل مهمل ويُحذف لاحقاً)
            migrationBuilder.Sql("UPDATE PlatformSettings SET HifzRatingPassingEvaluations = N'ممتاز,جيد جداً,جيد,مقبول'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableDress",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "EnableMatn",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "EnableParentHome",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "EnablePrayer",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsAttendanceExcused",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsAttendanceLate",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsAttendanceUnexcused",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsHifzMemGood",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsHifzMemVeryGood",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsHifzRevAcceptable",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsHifzRevGood",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsHifzRevVeryGood",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsMatnMemorization",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsAwarded",
                table: "MatnRecords");
        }
    }
}
