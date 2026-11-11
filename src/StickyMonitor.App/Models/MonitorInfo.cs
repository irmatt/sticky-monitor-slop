using System.Windows;

namespace StickyMonitor.App.Models;

public sealed record MonitorInfo(
    string MonitorId,
    string DisplayName,
    string ModelName,
    string DeviceName,
    Rect Bounds,
    Rect WorkArea,
    System.Windows.Size Resolution,
    string Orientation,
    bool IsSticky);
