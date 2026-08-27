using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupervisorPedagogicalFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultMonthlyAthmanTarget",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "OralAlertPenalty",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OralHesitationPenalty",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "OralMaxOpeningsBeforeFail",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "OralOpeningPenalty",
                table: "PlatformSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsExtra",
                table: "Circles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentCircleId",
                table: "Circles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SessionDate",
                table: "Circles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CircleId",
                table: "Attendances",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CircleEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CircleId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircleEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CircleEnrollments_Circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CircleEnrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DressRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCompliant = table.Column<bool>(type: "bit", nullable: false),
                    ScoreOutOf10 = table.Column<double>(type: "float", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DressRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DressRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CircleId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationPeriods_Circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MatnRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MatnName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Portion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Evaluation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatnRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatnRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OralExamSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CircleId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScopeLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxOpeningsBeforeFail = table.Column<int>(type: "int", nullable: false),
                    OverallPercent = table.Column<double>(type: "float", nullable: false),
                    OverallGrade = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsConsideredMemorized = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OralExamSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OralExamSessions_Circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OralExamSessions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParentHomeFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    WeekStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rating = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentHomeFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentHomeFeedbacks_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentHomeFeedbacks_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrayerDailyLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrayedInMosque = table.Column<bool>(type: "bit", nullable: false),
                    OnTime = table.Column<bool>(type: "bit", nullable: false),
                    MosquePrayerCount = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StudentNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SheikhOverrideNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverriddenByUserId = table.Column<int>(type: "int", nullable: true),
                    OverriddenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrayerDailyLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrayerDailyLogs_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentMonthlyTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    TargetAthmanCount = table.Column<int>(type: "int", nullable: false),
                    AchievedAthmanCount = table.Column<int>(type: "int", nullable: false),
                    ProgressScoreOutOf10 = table.Column<double>(type: "float", nullable: false),
                    IsSpecialMode = table.Column<bool>(type: "bit", nullable: false),
                    SpecialModeNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMonthlyTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentMonthlyTargets_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentPeriodEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AttendanceScore = table.Column<double>(type: "float", nullable: false),
                    HifzScore = table.Column<double>(type: "float", nullable: false),
                    ProgressScore = table.Column<double>(type: "float", nullable: false),
                    MatnScore = table.Column<double>(type: "float", nullable: false),
                    DressScore = table.Column<double>(type: "float", nullable: false),
                    OverallScore = table.Column<double>(type: "float", nullable: false),
                    GradeLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SheikhNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrayerAdvisoryScore = table.Column<double>(type: "float", nullable: true),
                    ParentHomeAdvisoryScore = table.Column<double>(type: "float", nullable: true),
                    IncludeAdvisoryInOverall = table.Column<bool>(type: "bit", nullable: false),
                    EvaluatedByUserId = table.Column<int>(type: "int", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPeriodEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPeriodEvaluations_EvaluationPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "EvaluationPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentPeriodEvaluations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OralExamQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HesitationCount = table.Column<int>(type: "int", nullable: false),
                    AlertCount = table.Column<int>(type: "int", nullable: false),
                    OpeningCount = table.Column<int>(type: "int", nullable: false),
                    ScorePercent = table.Column<double>(type: "float", nullable: false),
                    Impression = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OralExamQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OralExamQuestions_OralExamSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "OralExamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Circles_ParentCircleId",
                table: "Circles",
                column: "ParentCircleId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_CircleId",
                table: "Attendances",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_CircleEnrollment_Circle_Student",
                table: "CircleEnrollments",
                columns: new[] { "CircleId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CircleEnrollments_StudentId",
                table: "CircleEnrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Dress_Student_Date",
                table: "DressRecords",
                columns: new[] { "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationPeriods_CircleId",
                table: "EvaluationPeriods",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_MatnRecords_StudentId",
                table: "MatnRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_OralExamQuestions_SessionId",
                table: "OralExamQuestions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_OralExam_Student_Date",
                table: "OralExamSessions",
                columns: new[] { "StudentId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_OralExamSessions_CircleId",
                table: "OralExamSessions",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentHome_Student_Week",
                table: "ParentHomeFeedbacks",
                columns: new[] { "StudentId", "WeekStartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentHomeFeedbacks_ParentId",
                table: "ParentHomeFeedbacks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Prayer_Student_Date",
                table: "PrayerDailyLogs",
                columns: new[] { "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTarget_Student_YearMonth",
                table: "StudentMonthlyTargets",
                columns: new[] { "StudentId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodEval_Period_Student",
                table: "StudentPeriodEvaluations",
                columns: new[] { "PeriodId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPeriodEvaluations_StudentId",
                table: "StudentPeriodEvaluations",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Circles_CircleId",
                table: "Attendances",
                column: "CircleId",
                principalTable: "Circles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Circles_Circles_ParentCircleId",
                table: "Circles",
                column: "ParentCircleId",
                principalTable: "Circles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Circles_CircleId",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_Circles_Circles_ParentCircleId",
                table: "Circles");

            migrationBuilder.DropTable(
                name: "CircleEnrollments");

            migrationBuilder.DropTable(
                name: "DressRecords");

            migrationBuilder.DropTable(
                name: "MatnRecords");

            migrationBuilder.DropTable(
                name: "OralExamQuestions");

            migrationBuilder.DropTable(
                name: "ParentHomeFeedbacks");

            migrationBuilder.DropTable(
                name: "PrayerDailyLogs");

            migrationBuilder.DropTable(
                name: "StudentMonthlyTargets");

            migrationBuilder.DropTable(
                name: "StudentPeriodEvaluations");

            migrationBuilder.DropTable(
                name: "OralExamSessions");

            migrationBuilder.DropTable(
                name: "EvaluationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Circles_ParentCircleId",
                table: "Circles");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_CircleId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "DefaultMonthlyAthmanTarget",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "OralAlertPenalty",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "OralHesitationPenalty",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "OralMaxOpeningsBeforeFail",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "OralOpeningPenalty",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "IsExtra",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "ParentCircleId",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "SessionDate",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "CircleId",
                table: "Attendances");
        }
    }
}
