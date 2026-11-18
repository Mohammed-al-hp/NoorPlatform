using FluentAssertions;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using Xunit;

namespace NoorPlatform.Tests.Unit;

/// <summary>سلّم التقديرات المعتمد ونقاط الحضور والتقديرات والمتون.</summary>
public class GradeScaleAndPointsTests
{
    [Theory]
    [InlineData(100, "ممتاز")]
    [InlineData(85, "ممتاز")]
    [InlineData(84, "جيد جداً")]
    [InlineData(75, "جيد جداً")]
    [InlineData(74, "جيد")]
    [InlineData(65, "جيد")]
    [InlineData(64, "مقبول")]
    [InlineData(50, "مقبول")]
    [InlineData(49, "ضعيف ويحتاج مراجعة")]
    [InlineData(0, "ضعيف ويحتاج مراجعة")]
    public void Revision_FromPercent_FollowsApprovedRanges(double percent, string expected) =>
        GradeScale.FromPercent(percent, RecordType.Revision).Should().Be(expected);

    [Theory]
    [InlineData(85, "ممتاز")]
    [InlineData(65, "جيد")]
    [InlineData(64, "ضعيف ويحتاج مراجعة")] // الحفظ الجديد بلا «مقبول»
    [InlineData(50, "ضعيف ويحتاج مراجعة")]
    public void Memorization_FromPercent_HasNoAcceptable(double percent, string expected) =>
        GradeScale.FromPercent(percent, RecordType.Memorization).Should().Be(expected);

    [Fact]
    public void MemorizationGrades_ExcludeAcceptable()
    {
        GradeScale.MemorizationGrades.Should().NotContain(GradeScale.Acceptable);
        GradeScale.RevisionGrades.Should().Contain(GradeScale.Acceptable);
    }

    [Theory]
    [InlineData("ممتاز", true)]
    [InlineData("جيد جدًا", true)]
    [InlineData("جيد", true)]
    [InlineData("مقبول", true)]
    [InlineData("ضعيف ويحتاج مراجعة", false)]
    [InlineData("ضعيف", false)]
    [InlineData("يحتاج مراجعة", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPassing_WeakIsAFailedSession(string? evaluation, bool expected) =>
        GradeScale.IsPassing(evaluation).Should().Be(expected);

    [Fact]
    public void HifzPoints_DependOnGradeAndType()
    {
        var s = new PlatformSettings
        {
            PointsHifzMemorization = 50, PointsHifzMemVeryGood = 40, PointsHifzMemGood = 30,
            PointsHifzRevision = 20, PointsHifzRevVeryGood = 15, PointsHifzRevGood = 10, PointsHifzRevAcceptable = 5
        };
        PointsLedger.ForHifz(RecordType.Memorization, "ممتاز", s).Should().Be(50);
        PointsLedger.ForHifz(RecordType.Memorization, "جيد جداً", s).Should().Be(40);
        PointsLedger.ForHifz(RecordType.Memorization, "جيد", s).Should().Be(30);
        PointsLedger.ForHifz(RecordType.Memorization, "ضعيف ويحتاج مراجعة", s).Should().Be(0);
        PointsLedger.ForHifz(RecordType.Revision, "مقبول", s).Should().Be(5);
        PointsLedger.ForHifz(RecordType.Revision, "جيد", s).Should().Be(10);
        PointsLedger.ForHifz(RecordType.Revision, "ضعيف", s).Should().Be(0);
    }

    [Fact]
    public void AttendancePoints_CoverEveryStatus_AndAllowDeduction()
    {
        var s = new PlatformSettings
        {
            PointsAttendancePresent = 10, PointsAttendanceLate = 5,
            PointsAttendanceExcused = 0, PointsAttendanceUnexcused = -5
        };
        PointsLedger.ForAttendance(AttendanceStatus.Present, s).Should().Be(10);
        PointsLedger.ForAttendance(AttendanceStatus.Late, s).Should().Be(5);
        PointsLedger.ForAttendance(AttendanceStatus.ExcusedAbsence, s).Should().Be(0);
        PointsLedger.ForAttendance(AttendanceStatus.UnexcusedAbsence, s).Should().Be(-5);

        // الخصم لا ينزل برصيد الطالب عن الصفر
        var student = new Student { Points = 3 };
        PointsLedger.Apply(student, 0, -5);
        student.Points.Should().Be(0);
    }

    [Fact]
    public void MatnPoints_OnlyForPassingMemorization_AndWhenEnabled()
    {
        var s = new PlatformSettings { PointsMatnMemorization = 20, EnableMatn = true };
        PointsLedger.ForMatn(MatnRecordType.Memorization, "جيد", s).Should().Be(20);
        PointsLedger.ForMatn(MatnRecordType.Memorization, "ضعيف ويحتاج مراجعة", s).Should().Be(0);
        PointsLedger.ForMatn(MatnRecordType.Revision, "ممتاز", s).Should().Be(0);

        s.EnableMatn = false;
        PointsLedger.ForMatn(MatnRecordType.Memorization, "ممتاز", s).Should().Be(0);
    }
}
