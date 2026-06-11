namespace Sunglasses.Models;

/// <summary>
/// Persisted user settings for the Sunglasses overlay.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Dimming level of the black overlay, where 0.0 is fully transparent
    /// (no dimming) and 1.0 is fully opaque (black screen).
    /// </summary>
    public double Opacity { get; set; } = 0.2;
}
