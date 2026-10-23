namespace NoorPlatform.Core.Services;

public static class LibyaTime
{
    public static DateTime Now => DateTime.UtcNow.AddHours(2);
    public static DateTime Today => Now.Date;
}