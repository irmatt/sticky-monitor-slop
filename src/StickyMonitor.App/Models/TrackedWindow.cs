using System.Windows;

namespace StickyMonitor.App.Models;

public sealed record TrackedWindow(
    IntPtr Hwnd,
    string Title,
    string ProcessName,
    int Pid,
    string MonitorId,
    Rect Bounds,
    bool IsVisible,
    bool IsMinimized,
    bool IsFullscreen,
    bool IsEligible);
