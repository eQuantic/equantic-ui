using eQuantic.UI.Primitives;

namespace eQuantic.UI.Build;

/// <summary>
/// One cut of a family a provider serves: a weight, upright or italic, which is one file.
/// </summary>
/// <param name="Weight">The weight, from 1 to 1000.</param>
/// <param name="Italic">Whether it is the italic.</param>
public readonly record struct FontCut(int Weight, bool Italic) : IComparable<FontCut>
{
    /// <summary>The cut's key in the lock: <c>400</c>, or <c>700 italic</c>.</summary>
    public string Key => Italic ? $"{Weight} italic" : $"{Weight}";

    /// <summary>The cut in words, for a message: <c>Bold</c>, or <c>Bold italic</c>.</summary>
    public string Described => (Enum.IsDefined(typeof(FontWeight), Weight) ? ((FontWeight)Weight).ToString() : $"weight {Weight}")
        + (Italic ? " italic" : "");

    /// <summary>The cut a lock key names, or null for a key that names none.</summary>
    public static FontCut? Parse(string key)
    {
        var parts = key.Split(' ');
        return parts.Length switch
        {
            1 when int.TryParse(parts[0], out var weight) && weight is >= 1 and <= 1000 => new FontCut(weight, false),
            2 when parts[1] == "italic" && int.TryParse(parts[0], out var weight) && weight is >= 1 and <= 1000 => new FontCut(weight, true),
            _ => null,
        };
    }

    /// <summary>Upright before italic, then the lightest first: the order Google's API asks them in.</summary>
    public int CompareTo(FontCut other) => Italic != other.Italic ? Italic.CompareTo(other.Italic) : Weight.CompareTo(other.Weight);
}
