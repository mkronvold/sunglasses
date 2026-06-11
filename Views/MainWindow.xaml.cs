using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Sunglasses.Platform;
using Sunglasses.Services;
using Color = System.Windows.Media.Color;

namespace Sunglasses.Views;

/// <summary>
/// The fullscreen, click-through black overlay that dims the display and
/// shows the transient transparency on-screen display (OSD).
/// </summary>
public partial class MainWindow : Window
{
    private readonly TransparencyService _transparency;
    private readonly OsdService _osd;

    private IntPtr _hwnd = IntPtr.Zero;

    // Insert-after handle to (re)assert top-most z-order.
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    // The window itself stays fully opaque (Opacity = 1.0); dimming is applied
    // through the alpha of this black background brush so the white OSD text
    // remains fully visible regardless of the dimming level.
    private readonly SolidColorBrush _dimBrush = new(Color.FromArgb(0, 0, 0, 0));

    public MainWindow(TransparencyService transparency, OsdService osd)
    {
        InitializeComponent();

        _transparency = transparency;
        _osd = osd;

        Background = _dimBrush;
        ApplyDim(_transparency.CurrentOpacity);

        _transparency.OpacityChanged += OnOpacityChanged;
        _osd.Hide += () => OsdText.Visibility = Visibility.Collapsed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        ApplyClickThroughStyles(_hwnd);
        CoverVirtualScreen(_hwnd);
    }

    /// <summary>
    /// Re-asserts the overlay's geometry, top-most z-order, click-through styles
    /// and dim level. Call this after events that can invalidate the overlay,
    /// such as session unlock, display changes, or resume from sleep.
    /// </summary>
    public void RecoverCoverage()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        // The window may have been hidden or lost its layered content; make sure
        // it is shown and its WPF Topmost flag is set before re-asserting Win32 state.
        if (!IsVisible)
        {
            Show();
        }

        Topmost = true;

        ApplyClickThroughStyles(_hwnd);
        CoverVirtualScreen(_hwnd);
        ApplyDim(_transparency.CurrentOpacity);
    }

    private static void ApplyClickThroughStyles(IntPtr hwnd)
    {
        // Make the overlay click-through so interactions pass to apps underneath.
        int exStyle = Win32Interop.GetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE);
        exStyle |= Win32Interop.WS_EX_LAYERED | Win32Interop.WS_EX_TRANSPARENT | Win32Interop.WS_EX_TOOLWINDOW;
        Win32Interop.SetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Sizes the window to the entire virtual desktop using physical-pixel
    /// metrics via SetWindowPos, and re-asserts top-most z-order. This is
    /// DPI-safe across mixed-DPI monitors, unlike setting WPF Left/Top/Width/Height
    /// which are in DIPs.
    /// </summary>
    private static void CoverVirtualScreen(IntPtr hwnd)
    {
        int x = Win32Interop.GetSystemMetrics(Win32Interop.SM_XVIRTUALSCREEN);
        int y = Win32Interop.GetSystemMetrics(Win32Interop.SM_YVIRTUALSCREEN);
        int cx = Win32Interop.GetSystemMetrics(Win32Interop.SM_CXVIRTUALSCREEN);
        int cy = Win32Interop.GetSystemMetrics(Win32Interop.SM_CYVIRTUALSCREEN);

        Win32Interop.SetWindowPos(
            hwnd, HWND_TOPMOST, x, y, cx, cy,
            Win32Interop.SWP_NOACTIVATE);
    }

    private void ApplyDim(double level)
    {
        byte alpha = (byte)Math.Round(Math.Clamp(level, 0.0, 1.0) * 255.0);
        _dimBrush.Color = Color.FromArgb(alpha, 0, 0, 0);
    }

    private void OnOpacityChanged(double opacity)
    {
        ApplyDim(opacity);
        OsdText.Text = $"{_transparency.CurrentPercent}%";
        OsdText.Visibility = Visibility.Visible;
        _osd.Trigger();
    }
}
