using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickyMonitor.App.Models;
using StickyMonitor.App.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfApplication = System.Windows.Application;

namespace StickyMonitor.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly MonitorManager _monitorManager;
    private readonly StickyManager _stickyManager;
    private readonly StartupManager _startupManager;
    private bool _syncingSelectionState;
    private CancellationTokenSource? _manualApplyFeedbackCts;

    public MainViewModel(
        MonitorManager monitorManager,
        StickyManager stickyManager,
        StartupManager startupManager,
        StickySettings settings)
    {
        _monitorManager = monitorManager;
        _stickyManager = stickyManager;
        _startupManager = startupManager;

        _launchAtLogin = settings.LaunchAtLogin;
        _isStickyModeEnabled = settings.StickyModeEnabled;
        _isInstantDetectionEnabled = settings.InstantWindowDetection;
        _isManualMode = settings.TrackingMode == TrackingMode.Manual;
        _selectedCheckInterval = settings.BackgroundCheckSeconds;

        SelectMonitorCommand = new RelayCommand<MonitorCardViewModel>(SelectMonitor);
        ApplySelectedMonitorCommand = new AsyncRelayCommand(ApplySelectedMonitorAsync, CanApplySelectedMonitor);

        _monitorManager.MonitorsChanged += (_, _) => WpfApplication.Current.Dispatcher.Invoke(Refresh);
        _stickyManager.ConfigurationChanged += (_, _) => WpfApplication.Current.Dispatcher.Invoke(Refresh);
    }

    public ObservableCollection<MonitorCardViewModel> Monitors { get; } = [];

    [ObservableProperty]
    private double _canvasWidth = 1000;

    [ObservableProperty]
    private double _canvasHeight = 520;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _launchAtLogin;

    [ObservableProperty]
    private bool _isStickyModeEnabled;

    [ObservableProperty]
    private bool _isInstantDetectionEnabled;

    [ObservableProperty]
    private bool _isManualMode;

    [ObservableProperty]
    private int _selectedCheckInterval;

    [ObservableProperty]
    private MonitorCardViewModel? _selectedMonitor;

    [ObservableProperty]
    private bool _selectedMonitorPinned;

    [ObservableProperty]
    private bool _showManualApplyFeedback;

    [ObservableProperty]
    private string _manualApplyFeedbackText = string.Empty;

    public IReadOnlyList<int> CheckIntervalOptions { get; } = [2, 4, 10, 30, 60];

    public ICommand SelectMonitorCommand { get; }
    public IAsyncRelayCommand ApplySelectedMonitorCommand { get; }

    public bool IsAutoMode => !IsManualMode;
    public bool IsAutoModeSelected
    {
        get => !IsManualMode;
        set
        {
            if (value)
            {
                IsManualMode = false;
            }
        }
    }

    public bool HasSelectedMonitor => SelectedMonitor is not null;
    public string StickyModeLabel => IsStickyModeEnabled ? "Sticky Mode ON" : "Sticky Mode OFF";
    public string SelectedMonitorName => SelectedMonitor?.DisplayName ?? "No monitor selected";
    public string SelectedMonitorMeta => SelectedMonitor is null
        ? "Choose a monitor in the layout to manage its state."
        : string.IsNullOrWhiteSpace(SelectedMonitor.ModelName)
            ? SelectedMonitor.ResolutionLabel
            : $"{SelectedMonitor.ModelName} • {SelectedMonitor.ResolutionLabel}";
    public string SelectedMonitorOrientation => SelectedMonitor?.OrientationLabel ?? "Monitor details appear here.";
    public string SelectedMonitorStateLabel => SelectedMonitorPinned ? "Pinned across desktops" : "Not pinned";
    public string ModeDescription => IsManualMode
        ? "Manual mode applies changes only when you trigger them."
        : "Auto mode keeps pinned monitors enforced in the background.";
    public string AutoModeDetailsDescription => $"Sticky Monitor rechecks window positions every {SelectedCheckInterval} second{(SelectedCheckInterval == 1 ? string.Empty : "s")} while it runs in the background.";
    public string ManualModeDetailsDescription => SelectedMonitor is null
        ? "Apply sticky behavior across all monitors currently marked sticky."
        : $"Apply sticky behavior across all pinned monitors. {SelectedMonitor.DisplayName} is your current selection.";
    public string ManualApplyButtonText => ShowManualApplyFeedback ? "Applied" : "Apply Now";

    partial void OnLaunchAtLoginChanged(bool value)
    {
        _startupManager.SetEnabled(value);
        _stickyManager.Settings.LaunchAtLogin = value;
        _stickyManager.SaveSettings();
    }

    partial void OnIsStickyModeEnabledChanged(bool value)
    {
        _stickyManager.SetStickyModeEnabled(value);
        OnPropertyChanged(nameof(StickyModeLabel));
        ApplySelectedMonitorCommand.NotifyCanExecuteChanged();
        UpdateStatusText();
    }

    partial void OnIsInstantDetectionEnabledChanged(bool value)
    {
        _stickyManager.SetInstantDetectionEnabled(value);
    }

    partial void OnIsManualModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAutoMode));
        OnPropertyChanged(nameof(IsAutoModeSelected));
        OnPropertyChanged(nameof(ModeDescription));
        OnPropertyChanged(nameof(ManualModeDetailsDescription));
        ApplyTrackingSettings();
        ApplySelectedMonitorCommand.NotifyCanExecuteChanged();
        UpdateStatusText();
    }

    partial void OnSelectedCheckIntervalChanged(int value)
    {
        if (value > 0)
        {
            OnPropertyChanged(nameof(AutoModeDetailsDescription));
            ApplyTrackingSettings();
        }
    }

    partial void OnSelectedMonitorChanged(MonitorCardViewModel? value)
    {
        foreach (var monitor in Monitors)
        {
            monitor.IsSelected = ReferenceEquals(monitor, value);
        }

        _syncingSelectionState = true;
        SelectedMonitorPinned = value?.IsSticky ?? false;
        _syncingSelectionState = false;

        OnPropertyChanged(nameof(HasSelectedMonitor));
        OnPropertyChanged(nameof(SelectedMonitorName));
        OnPropertyChanged(nameof(SelectedMonitorMeta));
        OnPropertyChanged(nameof(SelectedMonitorOrientation));
        OnPropertyChanged(nameof(SelectedMonitorStateLabel));
        OnPropertyChanged(nameof(ManualModeDetailsDescription));
        ApplySelectedMonitorCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMonitorPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectedMonitorStateLabel));

        if (_syncingSelectionState || SelectedMonitor is null)
        {
            return;
        }

        SelectedMonitor.SetSticky(value);
        _stickyManager.SetStickyMonitor(SelectedMonitor.MonitorId, value);
        UpdateStatusText();

    }

    public void Refresh()
    {
        var monitors = _stickyManager.GetMonitors();
        var selectedMonitorId = SelectedMonitor?.MonitorId;
        RebuildMonitorCards(monitors);
        SelectedMonitor = Monitors.FirstOrDefault(monitor => monitor.MonitorId == selectedMonitorId) ?? Monitors.FirstOrDefault();
        UpdateStatusText();
    }

    private void RebuildMonitorCards(IReadOnlyList<MonitorInfo> monitors)
    {
        Monitors.Clear();
        if (monitors.Count == 0)
        {
            CanvasWidth = 1000;
            CanvasHeight = 520;
            return;
        }

        const double padding = 80;

        var minLeft = monitors.Min(monitor => monitor.Bounds.Left);
        var minTop = monitors.Min(monitor => monitor.Bounds.Top);
        var maxRight = monitors.Max(monitor => monitor.Bounds.Right);
        var maxBottom = monitors.Max(monitor => monitor.Bounds.Bottom);
        var totalWidth = Math.Max(1, maxRight - minLeft);
        var totalHeight = Math.Max(1, maxBottom - minTop);

        CanvasWidth = totalWidth + padding * 2;
        CanvasHeight = totalHeight + padding * 2;

        foreach (var monitor in monitors)
        {
            Monitors.Add(new MonitorCardViewModel(monitor)
            {
                X = monitor.Bounds.X - minLeft + padding,
                Y = monitor.Bounds.Y - minTop + padding,
                Width = Math.Max(240, monitor.Bounds.Width),
                Height = Math.Max(150, monitor.Bounds.Height)
            });
        }
    }

    private void ApplyTrackingSettings()
    {
        if (SelectedCheckInterval <= 0)
        {
            return;
        }

        var mode = IsManualMode ? TrackingMode.Manual : TrackingMode.Automatic;
        _stickyManager.UpdateTrackingSettings(mode, SelectedCheckInterval);
    }

    private void SelectMonitor(MonitorCardViewModel? monitor)
    {
        if (monitor is not null)
        {
            SelectedMonitor = monitor;
        }
    }

    private bool CanApplySelectedMonitor()
    {
        return IsManualMode && IsStickyModeEnabled && SelectedMonitor is not null;
    }

    private Task ApplySelectedMonitorAsync()
    {
        if (!IsStickyModeEnabled)
        {
            return Task.CompletedTask;
        }

        _stickyManager.RequestImmediateRefresh();
        var stickyCount = Monitors.Count(monitor => monitor.IsSticky);
        var feedback = stickyCount switch
        {
            0 => "No pinned monitors to apply.",
            1 => "Applied sticky behavior to 1 pinned monitor.",
            _ => $"Applied sticky behavior to {stickyCount} pinned monitors."
        };
        _ = ShowManualApplyFeedbackAsync(feedback);
        return Task.CompletedTask;
    }

    partial void OnShowManualApplyFeedbackChanged(bool value)
    {
        OnPropertyChanged(nameof(ManualApplyButtonText));
    }

    private void UpdateStatusText()
    {
        if (!IsStickyModeEnabled)
        {
            StatusText = "Sticky mode is off. Saved monitor states remain, but enforcement is paused.";
            return;
        }

        var stickyCount = Monitors.Count(monitor => monitor.IsSticky);
        if (stickyCount == 0)
        {
            StatusText = "No monitors are pinned across desktops.";
            return;
        }

        StatusText = $"{stickyCount} monitor{(stickyCount == 1 ? string.Empty : "s")} pinned across desktops. {ModeDescription}";
    }

    private async Task ShowManualApplyFeedbackAsync(string message)
    {
        _manualApplyFeedbackCts?.Cancel();
        _manualApplyFeedbackCts?.Dispose();
        _manualApplyFeedbackCts = new CancellationTokenSource();
        var token = _manualApplyFeedbackCts.Token;

        ManualApplyFeedbackText = message;
        ShowManualApplyFeedback = true;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ShowManualApplyFeedback = false;
        ManualApplyFeedbackText = string.Empty;
    }
}

public sealed partial class MonitorCardViewModel : ObservableObject
{
    public MonitorCardViewModel(MonitorInfo monitor)
    {
        MonitorId = monitor.MonitorId;
        DisplayName = monitor.DisplayName;
        ModelName = monitor.ModelName;
        DeviceName = monitor.DeviceName;
        ResolutionLabel = $"{monitor.Resolution.Width:0} x {monitor.Resolution.Height:0}";
        OrientationLabel = monitor.Orientation;
        _isSticky = monitor.IsSticky;
        UpdateVisuals();
    }

    public string MonitorId { get; }
    public string DisplayName { get; }
    public string ModelName { get; }
    public string DeviceName { get; }
    public string ResolutionLabel { get; }
    public string OrientationLabel { get; }
    public string StatusLabel => IsSticky ? "Pinned across desktops" : "Not pinned";
    public Visibility StickyOutlineVisibility => IsSticky ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private bool _isSticky;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private MediaBrush _borderBrush = new SolidColorBrush(MediaColor.FromRgb(42, 54, 69));

    [ObservableProperty]
    private MediaBrush _cardBrush = new SolidColorBrush(MediaColor.FromRgb(21, 29, 39));

    [ObservableProperty]
    private MediaBrush _statusBrush = new SolidColorBrush(MediaColor.FromRgb(83, 95, 110));

    partial void OnIsStickyChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StickyOutlineVisibility));
        UpdateVisuals();
    }

    partial void OnIsSelectedChanged(bool value)
    {
        UpdateVisuals();
    }

    public void SetSticky(bool value)
    {
        IsSticky = value;
    }

    private void UpdateVisuals()
    {
        BorderBrush = IsSticky
            ? new SolidColorBrush(MediaColor.FromRgb(0, 255, 80))
            : new SolidColorBrush(MediaColor.FromRgb(42, 54, 69));
        CardBrush = IsSelected
            ? new SolidColorBrush(MediaColor.FromRgb(46, 58, 72))
            : new SolidColorBrush(MediaColor.FromRgb(35, 47, 61));
        StatusBrush = IsSticky
            ? new SolidColorBrush(MediaColor.FromRgb(0, 255, 80))
            : new SolidColorBrush(MediaColor.FromRgb(83, 95, 110));
    }
}
