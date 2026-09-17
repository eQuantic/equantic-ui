namespace eQuantic.UI.Primitives;

/// <summary>
/// A static 2D transform as COMPONENTS, not a matrix — the closed, realizable set (spec S1): applied
/// translate → rotate → scale, anchored at the element's center. Compose fluently:
/// <c>Transform2D.Rotate(3).Scale(1.02f)</c>. The web realizer emits the equivalent CSS transform
/// list; the native realizer builds the equivalent <c>Matrix2D</c> around the box center. Always
/// reach instances through the factories/combinators — <c>default</c> is not meaningful.
/// </summary>
public readonly record struct Transform2D(
    float TranslateX = 0,
    float TranslateY = 0,
    float RotationDegrees = 0,
    float ScaleX = 1,
    float ScaleY = 1)
{
    public static Transform2D Translate(float x, float y = 0) => new(TranslateX: x, TranslateY: y);
    public static Transform2D Rotate(float degrees) => new(RotationDegrees: degrees);
    public static Transform2D Scale(float uniform) => new(ScaleX: uniform, ScaleY: uniform);
    public static Transform2D Scale(float x, float y) => new(ScaleX: x, ScaleY: y);

    /// <summary>This transform with the translation components replaced.</summary>
    public Transform2D WithTranslate(float x, float y = 0) => this with { TranslateX = x, TranslateY = y };

    /// <summary>This transform with the rotation replaced.</summary>
    public Transform2D WithRotate(float degrees) => this with { RotationDegrees = degrees };

    /// <summary>This transform with the scale replaced (uniform).</summary>
    public Transform2D WithScale(float uniform) => this with { ScaleX = uniform, ScaleY = uniform };

    public bool IsIdentity =>
        TranslateX == 0 && TranslateY == 0 && RotationDegrees == 0 && ScaleX == 1 && ScaleY == 1;
}
