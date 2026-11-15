namespace eQuantic.UI.Primitives;

/// <summary>
/// A static 2D transform as COMPONENTS, not a matrix — the closed, realizable set (spec S1): applied
/// translate → rotate → scale, anchored at the element's center. Compose fluently:
/// <c>Transform2D.Rotate(3).Scale(1.02f)</c>. The web realizer emits the equivalent CSS transform
/// list; the native realizer builds the equivalent <c>Matrix2D</c> around the box center. Always
/// reach instances through the factories/combinators — <c>default</c> is not meaningful.
/// <para>
/// The translation has two parts that add up: units (<see cref="TranslateX"/>) and fractions of the
/// box's own laid-out size (<see cref="TranslateFractionX"/>), Flutter's <c>FractionalTranslation</c>.
/// <c>-1.3</c> moves the box 130% of its width to the left, whatever that width turns out to be: a
/// band that starts outside the button it crosses (#614).
/// </para>
/// </summary>
public readonly record struct Transform2D(
    float TranslateX = 0,
    float TranslateY = 0,
    float RotationDegrees = 0,
    float ScaleX = 1,
    float ScaleY = 1,
    float TranslateFractionX = 0,
    float TranslateFractionY = 0)
{
    public static Transform2D Translate(float x, float y = 0) => new(TranslateX: x, TranslateY: y);

    /// <summary>A translation in fractions of the box's own width and height: <c>1</c> is the whole
    /// width.</summary>
    public static Transform2D FractionalTranslate(float x, float y = 0) =>
        new(TranslateFractionX: x, TranslateFractionY: y);
    public static Transform2D Rotate(float degrees) => new(RotationDegrees: degrees);
    public static Transform2D Scale(float uniform) => new(ScaleX: uniform, ScaleY: uniform);
    public static Transform2D Scale(float x, float y) => new(ScaleX: x, ScaleY: y);

    /// <summary>This transform with the translation components replaced.</summary>
    public Transform2D WithTranslate(float x, float y = 0) => this with { TranslateX = x, TranslateY = y };

    /// <summary>This transform with the fractional translation replaced.</summary>
    public Transform2D WithFractionalTranslate(float x, float y = 0) =>
        this with { TranslateFractionX = x, TranslateFractionY = y };

    /// <summary>This transform with its fractions turned into units for a box of this size: what it
    /// draws as, once the box is laid out.</summary>
    public Transform2D Resolved(float width, float height) =>
        TranslateFractionX == 0 && TranslateFractionY == 0
            ? this
            : this with
            {
                TranslateX = TranslateX + TranslateFractionX * width,
                TranslateY = TranslateY + TranslateFractionY * height,
                TranslateFractionX = 0,
                TranslateFractionY = 0,
            };

    /// <summary>This transform with the rotation replaced.</summary>
    public Transform2D WithRotate(float degrees) => this with { RotationDegrees = degrees };

    /// <summary>This transform with the scale replaced (uniform).</summary>
    public Transform2D WithScale(float uniform) => this with { ScaleX = uniform, ScaleY = uniform };

    public bool IsIdentity =>
        TranslateX == 0 && TranslateY == 0 && RotationDegrees == 0 && ScaleX == 1 && ScaleY == 1
        && TranslateFractionX == 0 && TranslateFractionY == 0;
}
