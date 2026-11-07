using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPointsToPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsAttendancePresent",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "PointsHifzMemorization",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "PointsHifzRevision",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsAttendancePresent",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsHifzMemorization",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PointsHifzRevision",
                table: "PlatformSettings");
        }
    }
}
