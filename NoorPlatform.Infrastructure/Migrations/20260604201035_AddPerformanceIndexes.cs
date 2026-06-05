using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Students_ParentId",
                table: "Students",
                newName: "IX_Student_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_ExamResults_ExamId",
                table: "ExamResults",
                newName: "IX_ExamResult_ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_Date",
                table: "Attendances",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendance_Date",
                table: "Attendances");

            migrationBuilder.RenameIndex(
                name: "IX_Student_ParentId",
                table: "Students",
                newName: "IX_Students_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_ExamResult_ExamId",
                table: "ExamResults",
                newName: "IX_ExamResults_ExamId");
        }
    }
}
