using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using StickyMonitor.App.ViewModels;

namespace StickyMonitor.App;

public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;

    public MainWindow(MainViewModel viewModel, string iconPath)
    {
        InitializeComponent();
        DataContext = viewModel;
        if (File.Exists(iconPath))
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
        }
    }

    public bool AllowClose { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyDarkTitleBar();
    }

    public void CloseSilently()
    {
        AllowClose = true;
        Close();
    }

    private void ApplyDarkTitleBar()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
