using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_SecurityAndPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Circles_Teachers_TeacherId",
                table: "Circles");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ParentId",
                table: "Payments");

            migrationBuilder.RenameIndex(
                name: "IX_Circles_TeacherId",
                table: "Circles",
                newName: "IX_Circle_TeacherId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Circles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_DueDate",
                table: "Payments",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_ParentId_Status",
                table: "Payments",
                columns: new[] { "ParentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryItem_CreatedAt",
                table: "LibraryItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_User_UserName",
                table: "AspNetUsers",
                column: "UserName");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFeed_CreatedAt",
                table: "ActivityFeeds",
                column: "CreatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Circles_Teachers_TeacherId",
                table: "Circles",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Circles_Teachers_TeacherId",
                table: "Circles");

            migrationBuilder.DropIndex(
                name: "IX_Payment_DueDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payment_ParentId_Status",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_LibraryItem_CreatedAt",
                table: "LibraryItems");

            migrationBuilder.DropIndex(
                name: "IX_User_UserName",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_ActivityFeed_CreatedAt",
                table: "ActivityFeeds");

            migrationBuilder.RenameIndex(
                name: "IX_Circle_TeacherId",
                table: "Circles",
                newName: "IX_Circles_TeacherId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Circles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ParentId",
                table: "Payments",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Circles_Teachers_TeacherId",
                table: "Circles",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
