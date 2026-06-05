using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentGuardianFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GuardianRelationship",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDate",
                table: "Students",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Residence",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentPhone",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "RegistrationDate",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Residence",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudentPhone",
                table: "Students");
        }
    }
}
