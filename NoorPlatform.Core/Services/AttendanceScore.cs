using NoorPlatform.Core.Entities;
namespace NoorPlatform.Core.Services;

public static class AttendanceScore
{
    public const double LateWeight = 0.75;
    public const double ExcusedWeight = 0.5;

    public static double Weight(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => 1.0,
        AttendanceStatus.Late => LateWeight,
        AttendanceStatus.ExcusedAbsence => ExcusedWeight,
        _ => 0.0
    };

    public static double Percent(IEnumerable<AttendanceStatus> statuses, int decimals = 1)
    {
        var list = statuses.ToList();
        return list.Count == 0 ? 0 : Math.Round(list.Sum(Weight) / list.Count * 100, decimals);
    }
}