namespace SnmpVideoWall.Models;

public sealed class AppSettings
{
    public int PollIntervalSeconds { get; set; } = 1;
    public int SnmpTimeoutMilliseconds { get; set; } = 1500;
    public string DayStart { get; set; } = "08:00";
    public string NightStart { get; set; } = "02:00";
    public ThresholdSet Day { get; set; } = new();
    public ThresholdSet Night { get; set; } = new();
}

