using NoorPlatform.Core.Entities;

namespace NoorPlatform.Web.Models;

public class DashboardViewModel
{
    public int StudentCount { get; set; }
    public int TeacherCount { get; set; }
    public int CircleCount { get; set; }
    public double AttendancePercentage { get; set; }
    
    public List<HifzRecord> RecentHifzRecords { get; set; } = new List<HifzRecord>();
}
