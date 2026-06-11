using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Sunglasses.Models;
using Sunglasses.Platform;
using Sunglasses.Services;
using Sunglasses.Views;
using MessageBox = System.Windows.MessageBox;

namespace Sunglasses;

/// <summary>
/// Application entry point. Enforces a single instance, wires up the services
/// and overlay window, installs global hooks, and cleans everything up on exit.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = "Global\\SunglassesScreenDimmer";
    private static readonly TimeSpan OsdDuration = TimeSpan.FromSeconds(2);

    // Safety-net interval: low-level hooks can be silently dropped without any
    // session/power/display event firing, so we periodically re-assert state.
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromMinutes(3);

    private DispatcherTimer? _watchdogTimer;

    private Mutex? _instanceMutex;

    private ConfigService? _configService;
    private TransparencyService? _transparencyService;
    private OsdService? _osdService;
    private AutoStartService? _autoStartService;
    private TrayIconService? _trayIconService;
    private GlobalHookService? _hookService;
    private MainWindow? _overlay;
    private AdjustTransparencyWindow? _adjustWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enforce a single running instance.
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            Shutdown();
            return;
        }

        _instanceMutex = mutex;

        // Keep running even though the overlay window is hidden/click-through.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _configService = new ConfigService();
        AppConfig config = _configService.Load();

        _transparencyService = new TransparencyService(config.Opacity);
        _osdService = new OsdService(OsdDuration);

        _overlay = new MainWindow(_transparencyService, _osdService);
        _overlay.Show();

        // Persist transparency changes (debounced).
        _transparencyService.OpacityChanged += opacity =>
            _configService.SaveDebounced(new AppConfig { Opacity = opacity });

        // System tray icon and its menu actions.
        _autoStartService = new AutoStartService();
        _trayIconService = new TrayIconService();
        _trayIconService.ExitRequested += () => Dispatcher.Invoke(Shutdown);
        _trayIconService.AdjustRequested += () => Dispatcher.Invoke(ShowAdjustWindow);
        _trayIconService.AutoStartStateRequested += () =>
            _trayIconService!.SetAutoStartChecked(_autoStartService!.IsEnabled());
        _trayIconService.AutoStartToggleRequested += () =>
        {
            bool enabled = _autoStartService!.Toggle();
            _trayIconService!.SetAutoStartChecked(enabled);
        };
        _trayIconService.SetAutoStartChecked(_autoStartService.IsEnabled());

        // Global hotkeys (installed on this UI thread).
        _hookService = new GlobalHookService();
        _hookService.AdjustRequested += delta => _transparencyService.Adjust(delta);
        _hookService.ExitRequested += () => Dispatcher.Invoke(Shutdown);
        try
        {
            _hookService.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Sunglasses could not install global hotkeys, so RAlt+MouseWheel and RAlt+RCtrl+Q will not work. " +
                $"Use the tray icon to adjust transparency or exit.\n\n{ex.Message}",
                "Sunglasses",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Recover after events that can invalidate the overlay or silently drop
        // the low-level hooks (e.g. overnight lock/unlock, monitor sleep).
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        // Periodic safety net for hook drops that fire no event.
        _watchdogTimer = new DispatcherTimer { Interval = WatchdogInterval };
        _watchdogTimer.Tick += (_, _) => RecoverOnUiThread();
        _watchdogTimer.Start();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock
            || e.Reason == SessionSwitchReason.SessionLogon
            || e.Reason == SessionSwitchReason.ConsoleConnect
            || e.Reason == SessionSwitchReason.RemoteConnect)
        {
            Recover();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Recover();

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Recover();
        }
    }

    /// <summary>
    /// Re-asserts overlay coverage/top-most state and reinstalls the global
    /// hooks. Marshalled to the UI thread; SystemEvents callbacks arrive on a
    /// separate thread.
    /// </summary>
    private void Recover()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        Dispatcher.BeginInvoke(RecoverOnUiThread);
    }

    private void RecoverOnUiThread()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            _overlay?.RecoverCoverage();
        }
        catch
        {
            // Overlay may be tearing down; ignore.
        }

        try
        {
            _hookService?.Reinstall();
        }
        catch
        {
            // If reinstalling fails we simply leave hotkeys unavailable;
            // the tray menu still works.
        }
    }

    private void ShowAdjustWindow()
    {
        if (_adjustWindow is { IsLoaded: true })
        {
            _adjustWindow.Activate();
            return;
        }

        _adjustWindow = new AdjustTransparencyWindow(_transparencyService!);
        _adjustWindow.Closed += (_, _) => _adjustWindow = null;
        _adjustWindow.Show();
        _adjustWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _watchdogTimer?.Stop();

        // Remove hooks first so no callbacks fire during teardown.
        _hookService?.Dispose();
        _trayIconService?.Dispose();

        // Make sure the latest setting is written before exiting.
        _configService?.Flush();
        _configService?.Dispose();

        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }
}
