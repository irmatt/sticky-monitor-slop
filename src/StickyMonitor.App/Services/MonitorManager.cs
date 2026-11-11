using Microsoft.Win32;
using StickyMonitor.App.Models;
using StickyMonitor.App.Native;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using FormsScreen = System.Windows.Forms.Screen;

namespace StickyMonitor.App.Services;

public sealed class MonitorManager : IDisposable
{
    private readonly object _modelMapLock = new();
    private Dictionary<string, string> _modelMap = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _modelMapReady;
    private int _modelMapLoading;

    public MonitorManager()
    {
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        EnsureModelMapWarmup();
    }

    public event EventHandler? MonitorsChanged;

    public IReadOnlyList<MonitorInfo> GetMonitors(IReadOnlySet<string>? stickyMonitorIds = null)
    {
        EnsureModelMapWarmup();
        var modelMap = _modelMapReady ? _modelMap : EmptyModelMap;
        var monitors = new List<MonitorInfo>();
        foreach (var screen in FormsScreen.AllScreens.OrderBy(screen => screen.Bounds.Left).ThenBy(screen => screen.Bounds.Top))
        {
            var displayDevice = new NativeMethods.DisplayDevice { Cb = Marshal.SizeOf<NativeMethods.DisplayDevice>() };
            var modelName = screen.DeviceName;
            var stableId = screen.DeviceName;
            if (NativeMethods.EnumDisplayDevices(screen.DeviceName, 0, ref displayDevice, 0))
            {
                modelName = ResolveModelName(displayDevice, modelMap);
                if (!string.IsNullOrWhiteSpace(displayDevice.DeviceID))
                {
                    stableId = displayDevice.DeviceID;
                }
            }

            var devMode = new NativeMethods.DevMode { dmSize = (short)Marshal.SizeOf<NativeMethods.DevMode>() };
            var hasDevMode = NativeMethods.EnumDisplaySettings(screen.DeviceName, NativeMethods.EnumCurrentSettings, ref devMode);
            var resolution = hasDevMode
                ? new System.Windows.Size(devMode.dmPelsWidth, devMode.dmPelsHeight)
                : new System.Windows.Size(screen.Bounds.Width, screen.Bounds.Height);

            var bounds = hasDevMode
                ? new Rect(devMode.dmPositionX, devMode.dmPositionY, devMode.dmPelsWidth, devMode.dmPelsHeight)
                : new Rect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height);
            var workArea = new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
            var orientation = bounds.Height > bounds.Width ? "Portrait" : "Landscape";
            var isSticky = stickyMonitorIds?.Contains(stableId) == true;

            monitors.Add(new MonitorInfo(
                stableId,
                $"Monitor {monitors.Count + 1}",
                modelName,
                screen.DeviceName,
                bounds,
                workArea,
                resolution,
                orientation,
                isSticky));
        }

        return monitors.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToArray();
    }

    public string? GetMonitorIdForWindow(IntPtr hwnd)
    {
        var hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (hMonitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new NativeMethods.MonitorInfoEx { CbSize = (uint)Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
        return NativeMethods.GetMonitorInfo(hMonitor, ref info) ? ResolveStableId(info.SzDevice) : null;
    }

    private static string ResolveStableId(string deviceName)
    {
        var displayDevice = new NativeMethods.DisplayDevice { Cb = Marshal.SizeOf<NativeMethods.DisplayDevice>() };
        if (NativeMethods.EnumDisplayDevices(deviceName, 0, ref displayDevice, 0)
            && !string.IsNullOrWhiteSpace(displayDevice.DeviceID))
        {
            return displayDevice.DeviceID;
        }

        return deviceName;
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        EnsureModelMapWarmup(force: true);
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureModelMapWarmup(bool force = false)
    {
        if (_modelMapReady && !force)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _modelMapLoading, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var loadedMap = BuildMonitorModelMap();
                lock (_modelMapLock)
                {
                    _modelMap = loadedMap;
                    _modelMapReady = true;
                }

                MonitorsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
            }
            finally
            {
                Interlocked.Exchange(ref _modelMapLoading, 0);
            }
        });
    }

    private static Dictionary<string, string> BuildMonitorModelMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
            using var collection = searcher.Get();
            foreach (ManagementObject monitor in collection)
            {
                var instanceName = monitor["InstanceName"]?.ToString();
                if (string.IsNullOrWhiteSpace(instanceName))
                {
                    continue;
                }

                var bytes = monitor["UserFriendlyName"] as ushort[];
                var friendly = DecodeMonitorName(bytes);
                if (!string.IsNullOrWhiteSpace(friendly))
                {
                    result[instanceName] = friendly;
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private static string ResolveModelName(NativeMethods.DisplayDevice device, IReadOnlyDictionary<string, string> modelMap)
    {
        if (!string.IsNullOrWhiteSpace(device.DeviceID))
        {
            var key = device.DeviceID.ToUpperInvariant();
            var matched = modelMap.FirstOrDefault(item => item.Key.ToUpperInvariant().Contains(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matched.Value))
            {
                return matched.Value;
            }
        }

        return !string.IsNullOrWhiteSpace(device.DeviceString) ? device.DeviceString : device.DeviceName;
    }

    private static string DecodeMonitorName(ushort[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return string.Empty;
        }

        var chars = bytes
            .TakeWhile(value => value != 0)
            .Select(value => (char)value)
            .ToArray();
        return new string(chars).Trim();
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyModelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
