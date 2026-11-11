using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using StickyMonitor.App.Services;
using StickyMonitor.App.ViewModels;

namespace StickyMonitor.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Windows.Forms.ToolStripMenuItem? _statusMenuItem;
    private MainWindow? _mainWindow;
    private MonitorManager? _monitorManager;
    private WindowTracker? _windowTracker;
    private DesktopManager? _desktopManager;
    private StickyManager? _stickyManager;
    private SettingsStore? _settingsStore;
    private StartupManager? _startupManager;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (!AcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "app-icon.ico");
        _settingsStore = new SettingsStore();
        var settings = _settingsStore.Load();
        _startupManager = new StartupManager();
        _monitorManager = new MonitorManager();
        _desktopManager = new DesktopManager();
        _windowTracker = new WindowTracker(_monitorManager);
        _stickyManager = new StickyManager(_monitorManager, _windowTracker, _desktopManager, _settingsStore, settings);
        _viewModel = new MainViewModel(_monitorManager, _stickyManager, _startupManager, settings);
        _mainWindow = new MainWindow(_viewModel, iconPath);
        _mainWindow.Closing += MainWindow_Closing;

        InitializeNotifyIcon(iconPath);
        _stickyManager.Start();
        _stickyManager.ConfigurationChanged += (_, _) => Dispatcher.BeginInvoke(new Action(UpdateTrayStatus));
        _viewModel.Refresh();
        UpdateTrayStatus();

        var startMinimized = e.Args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));
        if (startMinimized)
        {
            HideMainWindow();
        }
        else
        {
            ShowMainWindow();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationRegistration?.Unregister(null);
        _activateEvent?.Dispose();
        _singleInstanceMutex?.Dispose();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _mainWindow?.CloseSilently();
        _stickyManager?.Dispose();
        _windowTracker?.Dispose();
        _desktopManager?.Dispose();
        _monitorManager?.Dispose();

        base.OnExit(e);
    }

    private bool AcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, @"Local\StickyMonitor.App.v1", out var createdNew);
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\StickyMonitor.App.v1.Activate");

        if (!createdNew)
        {
            _activateEvent.Set();
            return false;
        }

        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(ShowMainWindow)),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        return true;
    }

    private void InitializeNotifyIcon(string iconPath)
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        var openItem = menu.Items.Add("Open Sticky Monitor");
        openItem.Click += (_, _) => ShowMainWindow();

        _statusMenuItem = new System.Windows.Forms.ToolStripMenuItem("Sticky monitors: 0")
        {
            Enabled = false
        };
        menu.Items.Add(_statusMenuItem);

        var launchAtLoginItem = new System.Windows.Forms.ToolStripMenuItem("Launch At Login")
        {
            Checked = _startupManager?.IsEnabled() == true,
            CheckOnClick = true
        };
        launchAtLoginItem.CheckedChanged += (_, _) =>
        {
            _startupManager?.SetEnabled(launchAtLoginItem.Checked);
            if (_viewModel is not null)
            {
                _viewModel.LaunchAtLogin = launchAtLoginItem.Checked;
            }
        };
        menu.Items.Add(launchAtLoginItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var exitItem = menu.Items.Add("Exit");
        exitItem.Click += (_, _) => Shutdown();

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            Text = "Sticky Monitor",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_mainWindow?.AllowClose == true)
        {
            return;
        }

        e.Cancel = true;
        HideMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.ShowInTaskbar = true;
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _stickyManager?.SetWindowVisible(true);
        _viewModel?.Refresh();
    }

    private void HideMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _stickyManager?.SetWindowVisible(false);
        _mainWindow.Hide();
        _mainWindow.ShowInTaskbar = false;
    }

    private void UpdateTrayStatus()
    {
        if (_statusMenuItem is null || _stickyManager is null)
        {
            return;
        }

        if (!_stickyManager.Settings.StickyModeEnabled)
        {
            _statusMenuItem.Text = "Sticky mode: Off";
            return;
        }

        var count = _stickyManager.GetMonitors().Count(monitor => monitor.IsSticky);
        _statusMenuItem.Text = $"Sticky monitors: {count}";
    }
}
