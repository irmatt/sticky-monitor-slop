using System.Text.Json;
using System.IO;
using StickyMonitor.App.Models;

namespace StickyMonitor.App.Services;

public sealed class SettingsStore
{
    private readonly string _settingsPath;

    public SettingsStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyMonitor");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public StickySettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new StickySettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<StickySettings>(json) ?? new StickySettings();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load settings; using defaults.", ex);
            return new StickySettings();
        }
    }

    public void Save(StickySettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _settingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to save settings.", ex);
        }
    }
}
