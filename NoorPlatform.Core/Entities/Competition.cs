using System;
using System.Collections.Generic;

namespace NoorPlatform.Core.Entities;

public enum CompetitionLevel
{
    Internal, // على مستوى المركز/الحلقة
    Regional, // مسابقة الأوقاف / مستوى المنطقة
    National  // وطنية
}

public class Competition
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public CompetitionLevel Level { get; set; }
    public string Description { get; set; } = string.Empty;

    public List<CompetitionResult> Results { get; set; } = new();
}
