namespace eQuantic.UI.Charts;

/// <summary>The ticks a value axis shows: clean steps from <see cref="Min"/> to <see cref="Max"/>.</summary>
public sealed record ValueTicks(double Min, double Max, double Step)
{
    /// <summary>How many ticks, both ends included.</summary>
    public int Count => (int)Math.Floor((Max - Min) / Step + 0.5) + 1;

    /// <summary>The value at tick <paramref name="index"/> — multiplied, never accumulated, so the
    /// last tick lands on <see cref="Max"/> exactly on every target.</summary>
    public double At(int index) => Min + index * Step;

    public double Span => Max - Min;
}

/// <summary>
/// The "nice numbers" a value axis is labelled with. PARITY IS THE DESIGN CONSTRAINT: this runs as
/// C# on the server and on Photon and as the transpiled twin in the browser, and the two must agree
/// on every tick — so the magnitude is found by multiplying and dividing by ten rather than through
/// a logarithm (which the two runtimes may round a unit apart), the step is chosen from 1, 2, 5, 10,
/// and the bounds are floors and ceilings of exact quotients.
/// </summary>
public static class ValueScale
{
    /// <summary>Clean bounds and a clean step around [<paramref name="low"/>, <paramref name="high"/>],
    /// aiming for <paramref name="target"/> ticks.</summary>
    public static ValueTicks Nice(double low, double high, int target)
    {
        if (target < 2) target = 2;
        if (high < low)
        {
            var swap = low;
            low = high;
            high = swap;
        }

        if (high == low) high = low + 1;

        var rough = (high - low) / (target - 1);
        var magnitude = 1.0;
        while (rough >= magnitude * 10) magnitude *= 10;
        while (rough < magnitude) magnitude /= 10;

        var ratio = rough / magnitude;
        var step = (ratio < 1.5 ? 1 : ratio < 3 ? 2 : ratio < 7 ? 5 : 10) * magnitude;
        var min = Math.Floor(low / step) * step;
        var max = Math.Ceiling(high / step) * step;
        if (max <= min) max = min + step;
        return new ValueTicks(min, max, step);
    }

    /// <summary>Exactly <paramref name="ticks"/> ticks between two bounds an author fixed.</summary>
    public static ValueTicks Fixed(double min, double max, int ticks)
    {
        if (ticks < 2) ticks = 2;
        if (max <= min) max = min + 1;
        return new ValueTicks(min, max, (max - min) / (ticks - 1));
    }
}
