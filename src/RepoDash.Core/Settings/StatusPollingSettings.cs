namespace RepoDash.Core.Settings;

public class StatusPollingSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 90;
    public bool PauseWhenMinimized { get; set; } = true;
    public int MaxParallelRequests { get; set; } = 6;
    public int TopUsagePercentage { get; set; } = 25;
}
