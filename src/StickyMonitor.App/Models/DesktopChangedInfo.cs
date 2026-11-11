namespace StickyMonitor.App.Models;

public sealed record DesktopChangedInfo(Guid OldDesktopId, Guid NewDesktopId, DateTimeOffset Timestamp);
