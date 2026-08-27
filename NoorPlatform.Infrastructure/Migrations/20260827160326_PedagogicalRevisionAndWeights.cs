using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PedagogicalRevisionAndWeights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "RevisionScore",
                table: "StudentPeriodEvaluations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "EvaluationsVisibleToStudentsAndParents",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "WeightAttendance",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WeightDress",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WeightHifz",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WeightMatn",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WeightProgress",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WeightRevision",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevisionScore",
                table: "StudentPeriodEvaluations");

            migrationBuilder.DropColumn(
                name: "EvaluationsVisibleToStudentsAndParents",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WeightAttendance",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WeightDress",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WeightHifz",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WeightMatn",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WeightProgress",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WeightRevision",
                table: "PlatformSettings");
        }
    }
}
