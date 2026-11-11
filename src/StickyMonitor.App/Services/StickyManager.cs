using StickyMonitor.App.Models;
using StickyMonitor.App.Native;
using System.Collections.Concurrent;
using System.Windows;

namespace StickyMonitor.App.Services;

public sealed class StickyManager : IDisposable
{
    private readonly MonitorManager _monitorManager;
    private readonly WindowTracker _windowTracker;
    private readonly DesktopManager _desktopManager;
    private readonly SettingsStore _settingsStore;
    private readonly StickySettings _settings;
    private readonly WindowEventWatcher _windowEventWatcher;
    private readonly object _settingsLock = new();
    private readonly ConcurrentDictionary<nint, ManagedWindowState> _managedWindows = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _evaluationLock = new(1, 1);
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private Task? _loopTask;
    private volatile bool _windowVisible = true;
    private int _restoreGeneration;
    private int _lastDesktopIndex = -1;

    public StickyManager(
        MonitorManager monitorManager,
        WindowTracker windowTracker,
        DesktopManager desktopManager,
        SettingsStore settingsStore,
        StickySettings settings)
    {
        _monitorManager = monitorManager;
        _windowTracker = windowTracker;
        _desktopManager = desktopManager;
        _settingsStore = settingsStore;
        _settings = settings;
        _windowEventWatcher = new WindowEventWatcher(RequestImmediateRefresh);

        _monitorManager.MonitorsChanged += (_, _) => RequestImmediateRefresh();
    }

    public event EventHandler? ConfigurationChanged;

    public StickySettings Settings => _settings;

    public void Start()
    {
        MigrateLegacyMonitorIds();

        if (_settings.InstantWindowDetection)
        {
            _windowEventWatcher.Start();
        }

        _loopTask = Task.Run(RunAsync);
    }

    public void SetWindowVisible(bool isVisible)
    {
        _windowVisible = isVisible;
    }

    public void SetStickyMonitor(string monitorId, bool isSticky)
    {
        lock (_settingsLock)
        {
            if (isSticky)
            {
                _settings.StickyMonitorIds.Add(monitorId);
            }
            else
            {
                _settings.StickyMonitorIds.Remove(monitorId);
            }

            _settingsStore.Save(_settings);
        }

        if (_settings.StickyModeEnabled && _settings.TrackingMode == TrackingMode.Automatic)
        {
            RequestImmediateRefresh();
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetStickyModeEnabled(bool enabled)
    {
        if (_settings.StickyModeEnabled == enabled)
        {
            return;
        }

        _settings.StickyModeEnabled = enabled;
        SaveSettings();
        RequestImmediateRefresh();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetInstantDetectionEnabled(bool enabled)
    {
        if (_settings.InstantWindowDetection == enabled)
        {
            return;
        }

        _settings.InstantWindowDetection = enabled;
        SaveSettings();

        if (enabled)
        {
            _windowEventWatcher.Start();
        }
        else
        {
            _windowEventWatcher.Stop();
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateTrackingSettings(TrackingMode mode, int backgroundCheckSeconds)
    {
        _settings.TrackingMode = mode;
        _settings.BackgroundCheckSeconds = Math.Max(2, backgroundCheckSeconds);
        SaveSettings();
        RequestImmediateRefresh();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveSettings()
    {
        lock (_settingsLock)
        {
            _settingsStore.Save(_settings);
        }
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        return _monitorManager.GetMonitors(GetStickyMonitorIds());
    }

    public void RequestImmediateRefresh()
    {
        if (_wakeSignal.CurrentCount == 0)
        {
            _wakeSignal.Release();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        _windowEventWatcher.Dispose();
        _cts.Dispose();
        _evaluationLock.Dispose();
        _wakeSignal.Dispose();
    }

    private HashSet<string> GetStickyMonitorIds()
    {
        lock (_settingsLock)
        {
            return new HashSet<string>(_settings.StickyMonitorIds, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void MigrateLegacyMonitorIds()
    {
        try
        {
            var monitors = _monitorManager.GetMonitors();
            lock (_settingsLock)
            {
                var changed = false;
                foreach (var monitor in monitors)
                {
                    if (string.IsNullOrWhiteSpace(monitor.DeviceName)
                        || string.Equals(monitor.DeviceName, monitor.MonitorId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (_settings.StickyMonitorIds.Contains(monitor.DeviceName))
                    {
                        _settings.StickyMonitorIds.Remove(monitor.DeviceName);
                        _settings.StickyMonitorIds.Add(monitor.MonitorId);
                        changed = true;
                    }
                }

                if (changed)
                {
                    _settingsStore.Save(_settings);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to migrate legacy monitor identifiers.", ex);
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await EvaluateAsync();
            _lastDesktopIndex = _desktopManager.GetCurrentDesktopIndex();
        }
        catch (Exception ex)
        {
            AppLog.Error("Initial sticky evaluation failed.", ex);
        }

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_settings.TrackingMode == TrackingMode.Manual)
                {
                    await _wakeSignal.WaitAsync(_cts.Token);
                }
                else
                {
                    var seconds = _windowVisible ? 2 : _settings.BackgroundCheckSeconds;
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(seconds), _cts.Token);
                    var signalTask = _wakeSignal.WaitAsync(_cts.Token);
                    await Task.WhenAny(delayTask, signalTask);
                }

                await EvaluateAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error("Sticky loop iteration failed.", ex);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task EvaluateAsync()
    {
        if (!await _evaluationLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var stickyIds = GetStickyMonitorIds();
            var currentDesktopIndex = _desktopManager.GetCurrentDesktopIndex();
            if (currentDesktopIndex >= 0 && currentDesktopIndex != _lastDesktopIndex)
            {
                Interlocked.Increment(ref _restoreGeneration);
                _lastDesktopIndex = currentDesktopIndex;
            }

            var windows = _windowTracker.GetTrackedWindows(stickyIds);

            if (!_settings.StickyModeEnabled)
            {
                UnpinAllManagedWindows();
                return;
            }

            var generation = _restoreGeneration;
            var activeHandles = windows.Select(window => window.Hwnd).ToHashSet();

            foreach (var window in windows)
            {
                var shouldPin = stickyIds.Contains(window.MonitorId) && window.IsEligible;

                if (!shouldPin)
                {
                    if (_managedWindows.TryRemove(window.Hwnd, out var removed) && removed.Pinned)
                    {
                        _desktopManager.TryUnpinWindow(window.Hwnd);
                        _desktopManager.TryUnpinApplication(window.Pid);
                    }

                    continue;
                }

                if (!_managedWindows.TryGetValue(window.Hwnd, out var state))
                {
                    state = new ManagedWindowState(window.MonitorId, window.Bounds, generation);
                    _managedWindows[window.Hwnd] = state;
                }

                if (!state.Pinned && DateTime.UtcNow >= state.NextPinAttemptUtc)
                {
                    state.Pinned = _desktopManager.TryPinWindow(window.Hwnd) || _desktopManager.TryPinApplication(window.Pid);

                    if (state.Pinned)
                    {
                        state.PinFailures = 0;
                    }
                    else
                    {
                        // Some windows never pin: elevated ones, shell-owned ones, and windows with no
                        // application view. Retrying them every couple of seconds forever is what used
                        // to spin the helper process into an endless crash loop, so back off instead.
                        state.PinFailures++;
                        state.NextPinAttemptUtc = DateTime.UtcNow + PinRetryBackoff(state.PinFailures);
                    }
                }

                state.MonitorId = window.MonitorId;
                state.Bounds = window.Bounds;

                if (!state.Pinned && !window.IsMinimized && state.RestoreGeneration != generation)
                {
                    TryRestoreWindow(window, state.Bounds);
                    state.RestoreGeneration = generation;
                }
            }

            foreach (var stale in _managedWindows.Keys.Where(handle => !activeHandles.Contains(handle)).ToArray())
            {
                if (_managedWindows.TryRemove(stale, out var staleState) && staleState.Pinned)
                {
                    _desktopManager.TryUnpinWindow(stale);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Sticky evaluation failed.", ex);
        }
        finally
        {
            _evaluationLock.Release();
        }
    }

    private void UnpinAllManagedWindows()
    {
        foreach (var handle in _managedWindows.Keys.ToArray())
        {
            if (_managedWindows.TryRemove(handle, out var state) && state.Pinned)
            {
                _desktopManager.TryUnpinWindow(handle);
            }
        }
    }

    private static void TryRestoreWindow(TrackedWindow window, Rect bounds)
    {
        if (window.IsMinimized)
        {
            return;
        }

        _ = NativeMethods.ShowWindowAsync(window.Hwnd, NativeMethods.SwShownoactivate);
        _ = NativeMethods.SetWindowPos(
            window.Hwnd,
            IntPtr.Zero,
            (int)bounds.X,
            (int)bounds.Y,
            (int)bounds.Width,
            (int)bounds.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoOwnerZOrder);
    }

    private sealed class ManagedWindowState
    {
        public ManagedWindowState(string monitorId, Rect bounds, int restoreGeneration)
        {
            MonitorId = monitorId;
            Bounds = bounds;
            RestoreGeneration = restoreGeneration;
        }

        public string MonitorId;
        public Rect Bounds;
        public bool Pinned;
        public int RestoreGeneration;
        public int PinFailures;
        public DateTime NextPinAttemptUtc = DateTime.MinValue;
    }

    private static TimeSpan PinRetryBackoff(int failures)
    {
        // 2, 4, 8 then 15 minutes at the cap.
        var minutes = Math.Min(15, 2 * Math.Pow(2, Math.Min(failures - 1, 3)));
        return TimeSpan.FromMinutes(minutes);
    }
}
