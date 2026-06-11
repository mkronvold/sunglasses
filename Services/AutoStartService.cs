using System.Diagnostics;
using Microsoft.Win32;

namespace Sunglasses.Services;

/// <summary>
/// Manages whether Sunglasses launches automatically when the current user logs
/// in, via the per-user Run registry key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). No admin rights needed.
/// </summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Sunglasses";

    private readonly string _executablePath;

    public AutoStartService()
    {
        _executablePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? string.Empty;
    }

    /// <summary>True if an auto-start entry currently exists for this app.</summary>
    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is not null;
    }

    /// <summary>Creates/updates the auto-start entry to point at this executable.</summary>
    public void Enable()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, $"\"{_executablePath}\"");
    }

    /// <summary>Removes the auto-start entry if present.</summary>
    public void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Toggles auto-start and returns the resulting enabled state.</summary>
    public bool Toggle()
    {
        if (IsEnabled())
        {
            Disable();
            return false;
        }

        Enable();
        return true;
    }
}
