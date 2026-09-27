using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressThresholdsToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ProgressThreshold10",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ProgressThreshold4",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ProgressThreshold6",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ProgressThreshold8",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ProgressThreshold9",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressThreshold10",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ProgressThreshold4",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ProgressThreshold6",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ProgressThreshold8",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ProgressThreshold9",
                table: "PlatformSettings");
        }
    }
}
