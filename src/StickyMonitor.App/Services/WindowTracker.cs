using StickyMonitor.App.Models;
using StickyMonitor.App.Native;
using System.Windows;

namespace StickyMonitor.App.Services;

public sealed class WindowTracker : IDisposable
{
    private readonly MonitorManager _monitorManager;

    public WindowTracker(MonitorManager monitorManager)
    {
        _monitorManager = monitorManager;
    }

    public IReadOnlyList<TrackedWindow> GetTrackedWindows(IReadOnlySet<string> stickyMonitorIds)
    {
        var windows = new List<TrackedWindow>();
        var allMonitors = _monitorManager.GetMonitors(stickyMonitorIds).ToDictionary(item => item.MonitorId, item => item);

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsToolWindow(hwnd) || NativeMethods.IsCloaked(hwnd))
            {
                return true;
            }

            var (pid, processName) = NativeMethods.TryGetProcess(hwnd);
            if (pid == Environment.ProcessId)
            {
                return true;
            }

            var title = NativeMethods.GetWindowTitle(hwnd);
            var minimized = NativeMethods.IsIconic(hwnd);
            if (!NativeMethods.GetWindowRect(hwnd, out var rectNative))
            {
                return true;
            }

            var bounds = rectNative.ToRect();
            if (bounds.Width < 80 || bounds.Height < 60)
            {
                return true;
            }

            var monitorId = _monitorManager.GetMonitorIdForWindow(hwnd);
            if (string.IsNullOrWhiteSpace(monitorId) || !allMonitors.TryGetValue(monitorId, out var monitor))
            {
                return true;
            }

            var isFullscreen = IsFullscreenWindow(bounds, monitor.Bounds);
            var eligible = !minimized && !isFullscreen;

            windows.Add(new TrackedWindow(
                hwnd,
                string.IsNullOrWhiteSpace(title) ? processName : title,
                processName,
                pid,
                monitorId,
                bounds,
                true,
                minimized,
                isFullscreen,
                eligible));

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public void Dispose()
    {
    }

    private static bool IsFullscreenWindow(Rect bounds, Rect monitorBounds)
    {
        return Math.Abs(bounds.Width - monitorBounds.Width) < 4 &&
               Math.Abs(bounds.Height - monitorBounds.Height) < 4 &&
               Math.Abs(bounds.X - monitorBounds.X) < 4 &&
               Math.Abs(bounds.Y - monitorBounds.Y) < 4;
    }
}
