namespace StickyMonitor.App.Models;

public enum TrackingMode
{
    Automatic,
    Manual
}

public sealed class StickySettings
{
    public HashSet<string> StickyMonitorIds { get; set; } = [];
    public bool LaunchAtLogin { get; set; }
    public bool StickyModeEnabled { get; set; } = true;
    public bool InstantWindowDetection { get; set; } = true;
    public TrackingMode TrackingMode { get; set; } = TrackingMode.Automatic;
    public int BackgroundCheckSeconds { get; set; } = 4;
}
