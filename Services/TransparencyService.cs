namespace Sunglasses.Services;

/// <summary>
/// Holds and mutates the current overlay dimming level, clamped to [0, 1],
/// and notifies subscribers when it changes.
/// </summary>
public sealed class TransparencyService
{
    public const double Min = 0.0;
    public const double Max = 1.0;

    private double _currentOpacity;

    public TransparencyService(double initialOpacity)
    {
        _currentOpacity = Clamp(initialOpacity);
    }

    /// <summary>Current dimming level in the range [0, 1].</summary>
    public double CurrentOpacity => _currentOpacity;

    /// <summary>Current dimming level as an integer percentage [0, 100].</summary>
    public int CurrentPercent => (int)Math.Round(_currentOpacity * 100.0);

    /// <summary>Raised whenever the opacity value actually changes.</summary>
    public event Action<double>? OpacityChanged;

    /// <summary>Sets opacity to an absolute value, clamped to the valid range.</summary>
    public void SetOpacity(double value)
    {
        double clamped = Clamp(value);
        if (Math.Abs(clamped - _currentOpacity) < 0.00001)
        {
            return;
        }

        _currentOpacity = clamped;
        OpacityChanged?.Invoke(_currentOpacity);
    }

    /// <summary>Adjusts opacity by a signed delta.</summary>
    public void Adjust(double delta) => SetOpacity(_currentOpacity + delta);

    private static double Clamp(double value) => Math.Min(Max, Math.Max(Min, value));
}
