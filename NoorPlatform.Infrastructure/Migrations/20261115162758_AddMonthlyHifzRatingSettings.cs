using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyHifzRatingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HifzRatingPassingEvaluations",
                table: "PlatformSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "ممتاز,جيد جداً,جيد,ضعيف");

            migrationBuilder.AddColumn<int>(
                name: "HifzRatingTargetAge11Plus",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<int>(
                name: "HifzRatingTargetAge5To7",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "HifzRatingTargetAge8To10",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HifzRatingPassingEvaluations",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "HifzRatingTargetAge11Plus",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "HifzRatingTargetAge5To7",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "HifzRatingTargetAge8To10",
                table: "PlatformSettings");
        }
    }
}
