namespace eQuantic.UI.Primitives;

/// <summary>
/// A repeating hairline GRID: the "graph paper" backdrop marketing surfaces use behind a hero. Square
/// cells, one colour, rules that start at the placing area's origin (<see cref="Pattern.Area"/>) and
/// continue across the clip area on every side. The web writes it as two repeating
/// <c>linear-gradient</c> layers; Photon draws the rules as fills inside the clip area's rounded
/// rectangle.
/// </summary>
/// <param name="Cell">Grid spacing in dp (the design's hero uses 56).</param>
/// <param name="Color">Line color — a token, so the grid tracks light/dark like everything else.</param>
/// <param name="LineWidth">Line thickness in dp. 1 is the only value the design uses.</param>
/// <exception cref="ArgumentOutOfRangeException">A cell or a line width that is not a finite number
/// above 0.</exception>
public sealed record GridPattern(float Cell, ColorToken Color, float LineWidth = 1) : Pattern
{
    /// <summary>Grid spacing in dp: a finite number above 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Zero, negative or not finite.</exception>
    public float Cell { get; init => field = Refused(value, nameof(Cell)); } = Refused(Cell, nameof(Cell));

    /// <summary>Line thickness in dp: a finite number above 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Zero, negative or not finite.</exception>
    public float LineWidth { get; init => field = Refused(value, nameof(LineWidth)); } = Refused(LineWidth, nameof(LineWidth));

    /// <summary>
    /// <paramref name="dp"/>, or the refusal naming <paramref name="member"/>. The initializers write
    /// the backing fields without the init accessors, so the constructor and a <c>with</c> both ask
    /// here.
    /// </summary>
    private static float Refused(float dp, string member) => dp > 0 && float.IsFinite(dp)
        ? dp
        : throw new ArgumentOutOfRangeException(member, dp, member == nameof(Cell)
            ? "A grid's cell is the distance between its rules in dp, a finite number above 0."
            : "A grid's rule is a line in dp, a finite number above 0.");

    private protected override TResult Dispatch<TState, TResult>(IPatternVisitor<TState, TResult> visitor, TState state) =>
        visitor.Visit(this, state);
}
