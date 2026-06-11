using System.Drawing;
using System.Windows.Forms;

namespace Sunglasses.Services;

/// <summary>
/// Provides a system-tray icon with a right-click context menu
/// ("Adjust Transparency..." and "Exit"). The icon is generated at runtime
/// so no binary resource is required.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly Icon _icon;
    private bool _disposed;

    /// <summary>Raised when the user chooses "Adjust Transparency...".</summary>
    public event Action? AdjustRequested;

    /// <summary>Raised when the user toggles "Start with Windows".</summary>
    public event Action? AutoStartToggleRequested;

    /// <summary>
    /// Raised just before the menu opens so the host can refresh the
    /// "Start with Windows" checkmark to reflect the current state.
    /// </summary>
    public event Action? AutoStartStateRequested;

    /// <summary>Raised when the user chooses "Exit".</summary>
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _icon = CreateSunglassesIcon();

        _autoStartItem = new ToolStripMenuItem("Start with Windows", null,
            (_, _) => AutoStartToggleRequested?.Invoke())
        {
            CheckOnClick = false,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Adjust Transparency...", null, (_, _) => AdjustRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
        menu.Opening += (_, _) => AutoStartStateRequested?.Invoke();
        _menu = menu;

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "Sunglasses Screen Dimmer",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // Double-clicking the tray icon also opens the adjustment dialog.
        _notifyIcon.DoubleClick += (_, _) => AdjustRequested?.Invoke();
    }

    private static Icon CreateSunglassesIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var lensBrush = new SolidBrush(Color.Black);
            using var bridgePen = new Pen(Color.Black, 3);

            // Two lenses.
            g.FillEllipse(lensBrush, 2, 11, 12, 12);
            g.FillEllipse(lensBrush, 18, 11, 12, 12);
            // Bridge connecting the lenses.
            g.DrawLine(bridgePen, 13, 14, 19, 14);
        }

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            // Clone so the icon survives after DestroyIcon frees the GDI handle.
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    /// <summary>Updates the "Start with Windows" checkmark.</summary>
    public void SetAutoStartChecked(bool isChecked) => _autoStartItem.Checked = isChecked;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);
    }
}
