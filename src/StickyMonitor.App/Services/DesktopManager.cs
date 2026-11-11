using System.Diagnostics;
using StickyMonitor.App.Interop;
using VD = StickyMonitor.App.Interop;

namespace StickyMonitor.App.Services;

/// <summary>
/// Virtual desktop access, in-process.
///
/// Previous versions spawned vendor\VirtualDesktop11-24H2.exe once per call - several dozen
/// processes per evaluation. When the helper reached a window it could not resolve it threw an
/// unhandled exception, Windows Error Reporting put up an "Application Error" dialog, this app
/// killed the child after a 3 s timeout, and the window silently never got pinned. That is what
/// produced hundreds of WER reports and the error box that could block shutdown.
///
/// The COM plumbing now runs inside the app (Interop\VirtualDesktopInterop.cs), where a window
/// that cannot be pinned is simply a caught exception.
/// </summary>
public sealed class DesktopManager : IDisposable
{
    private static readonly TimeSpan ErrorLogThrottle = TimeSpan.FromMinutes(5);

    private DateTime _lastDesktopError = DateTime.MinValue;
    private DateTime _lastPinError = DateTime.MinValue;

    public int GetCurrentDesktopIndex()
    {
        try
        {
            return VD.Desktop.FromDesktop(VD.Desktop.Current);
        }
        catch (Exception ex)
        {
            ResetInterop();
            LogThrottled(ref _lastDesktopError, "Reading the current virtual desktop failed.", ex);
            return -1;
        }
    }

    /// <summary>
    /// Pins a window so it is visible on every virtual desktop. False when the window cannot be
    /// pinned (elevated windows and some shell windows refuse to yield an application view).
    /// </summary>
    public bool TryPinWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (VD.Desktop.IsWindowPinned(hwnd))
            {
                return true;
            }

            VD.Desktop.PinWindow(hwnd);
            return VD.Desktop.IsWindowPinned(hwnd);
        }
        catch (Exception ex)
        {
            LogThrottled(
                ref _lastPinError,
                $"Pin window 0x{hwnd.ToInt64():X} failed - the window probably cannot be pinned (elevated or shell-owned).",
                ex);
            return false;
        }
    }

    public bool TryUnpinWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!VD.Desktop.IsWindowPinned(hwnd))
            {
                return true;
            }

            VD.Desktop.UnpinWindow(hwnd);
            return true;
        }
        catch (Exception ex)
        {
            LogThrottled(ref _lastPinError, $"Unpin window 0x{hwnd.ToInt64():X} failed.", ex);
            return false;
        }
    }

    public bool TryPinApplication(int pid) => TryApplication(pid, pin: true);

    public bool TryUnpinApplication(int pid) => TryApplication(pid, pin: false);

    private bool TryApplication(int pid, bool pin)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (pin)
            {
                VD.Desktop.PinApplication(hwnd);
            }
            else
            {
                VD.Desktop.UnpinApplication(hwnd);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogThrottled(
                ref _lastPinError,
                $"{(pin ? "Pin" : "Unpin")} application (pid {pid}) failed.",
                ex);
            return false;
        }
    }

    /// <summary>
    /// Drops the cached shell COM objects. An Explorer restart invalidates them, and every call
    /// after that fails until they are rebuilt.
    /// </summary>
    private static void ResetInterop()
    {
        try
        {
            VD.DesktopManager.Reset();
        }
        catch
        {
        }
    }

    private static void LogThrottled(ref DateTime last, string message, Exception ex)
    {
        var now = DateTime.UtcNow;
        if (now - last < ErrorLogThrottle)
        {
            return;
        }

        last = now;
        AppLog.Warn($"{message} ({ex.GetType().Name}: {ex.Message})");
    }

    public void Dispose()
    {
        ResetInterop();
    }
}
