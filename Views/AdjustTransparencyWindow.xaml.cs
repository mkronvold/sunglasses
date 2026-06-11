using System.Windows;
using Sunglasses.Services;

namespace Sunglasses.Views;

/// <summary>
/// Small dialog (opened from the tray icon) with a slider to set the dimming
/// level directly. Updates the overlay live via <see cref="TransparencyService"/>.
/// </summary>
public partial class AdjustTransparencyWindow : Window
{
    private readonly TransparencyService _transparency;
    private bool _initializing;

    public AdjustTransparencyWindow(TransparencyService transparency)
    {
        InitializeComponent();

        _transparency = transparency;

        _initializing = true;
        OpacitySlider.Value = _transparency.CurrentPercent;
        PercentLabel.Text = $"{_transparency.CurrentPercent}%";
        _initializing = false;

        // Keep the slider in sync if transparency changes via hotkeys while open.
        _transparency.OpacityChanged += OnOpacityChanged;
        Closed += (_, _) => _transparency.OpacityChanged -= OnOpacityChanged;
    }

    private void OnOpacityChanged(double opacity)
    {
        _initializing = true;
        OpacitySlider.Value = _transparency.CurrentPercent;
        PercentLabel.Text = $"{_transparency.CurrentPercent}%";
        _initializing = false;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }

        _transparency.SetOpacity(e.NewValue / 100.0);
        PercentLabel.Text = $"{_transparency.CurrentPercent}%";
    }
}
