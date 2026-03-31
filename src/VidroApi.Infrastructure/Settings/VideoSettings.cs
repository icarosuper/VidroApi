namespace VidroApi.Infrastructure.Settings;

public class VideoSettings
{
    public int MaxTagsPerVideo { get; set; }
    public int ReconciliationIntervalMinutes { get; set; }
    public int ViewDeduplicationWindowHours { get; set; }
}
