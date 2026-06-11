using System.Windows.Threading;

namespace Sunglasses.Services;

/// <summary>
/// Manages the auto-hide timing for the on-screen transparency display.
/// Call <see cref="Trigger"/> each time the value changes; <see cref="Hide"/>
/// is raised once the display duration elapses with no further changes.
/// Runs on the WPF UI thread via a <see cref="DispatcherTimer"/>.
/// </summary>
public sealed class OsdService
{
    private readonly DispatcherTimer _timer;

    public OsdService(TimeSpan duration)
    {
        _timer = new DispatcherTimer { Interval = duration };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Hide?.Invoke();
        };
    }

    /// <summary>Raised when the OSD should be hidden.</summary>
    public event Action? Hide;

    /// <summary>Shows/refreshes the OSD by restarting the hide countdown.</summary>
    public void Trigger()
    {
        _timer.Stop();
        _timer.Start();
    }
}
