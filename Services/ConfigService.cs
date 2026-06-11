using System.IO;
using System.Text.Json;
using Sunglasses.Models;

namespace Sunglasses.Services;

/// <summary>
/// Loads and saves <see cref="AppConfig"/> as JSON under
/// %LocalAppData%\Sunglasses\config.json. Saves are debounced so rapid
/// transparency changes don't thrash the disk.
/// </summary>
public sealed class ConfigService : IDisposable
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _configPath;
    private readonly System.Threading.Timer _saveTimer;
    private readonly object _lock = new();

    private AppConfig _pending = new();
    private bool _disposed;

    public ConfigService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sunglasses");
        _configPath = Path.Combine(dir, "config.json");
        _saveTimer = new System.Threading.Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Loads the configuration, returning defaults if the file is missing or corrupt.
    /// The loaded value also seeds the pending state so a later flush without any
    /// change won't overwrite the saved setting with defaults.
    /// </summary>
    public AppConfig Load()
    {
        AppConfig result = new();
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config is not null)
                {
                    result = config;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable config: fall back to defaults.
        }

        lock (_lock)
        {
            _pending = result;
        }

        return result;
    }

    /// <summary>Schedules a debounced save of the given configuration.</summary>
    public void SaveDebounced(AppConfig config)
    {
        lock (_lock)
        {
            _pending = config;
        }

        _saveTimer.Change(SaveDebounce, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Writes the most recent pending configuration to disk immediately.</summary>
    public void Flush()
    {
        // Serialize the entire read-and-write under the lock so a timer-triggered
        // flush and an explicit exit flush cannot write the file concurrently.
        lock (_lock)
        {
            AppConfig config = _pending;
            try
            {
                string? dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(_configPath, json);
            }
            catch
            {
                // Best-effort persistence; ignore write failures.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _saveTimer.Dispose();
    }
}
