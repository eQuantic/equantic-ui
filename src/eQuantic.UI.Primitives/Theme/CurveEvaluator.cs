namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="Curve"/> MEANS at a moment: the eased progress for a linear one.
/// <para>
/// The curves were data without an evaluator — four control points nobody could ask a question of.
/// The native transition store stood in a smoothstep for all of them and said so in a comment, so
/// every animation on Photon moved on the same curve while the web moved on the real one the token
/// names. Two targets, one authored intent, two different motions.
/// </para>
/// <para>
/// A CSS <c>cubic-bezier(x1, y1, x2, y2)</c> is a parametric curve whose x is TIME and y is
/// progress, with the endpoints pinned at (0,0) and (1,1). Asking "what progress at time t" means
/// finding the parameter s where x(s) = t, then reading y(s) — there is no closed form, so this
/// does what every browser does: Newton-Raphson from a good guess, falling back to bisection where
/// the curve is too flat for the derivative to help.
/// </para>
/// </summary>
public static class CurveEvaluator
{
    // Browsers converge in a handful of steps; more iterations buy nothing a pixel can show.
    private const int NewtonIterations = 8;
    private const float NewtonMinSlope = 0.001f;
    private const float Precision = 1e-6f;
    private const int BisectionIterations = 24;

    /// <summary>
    /// The eased progress 0..1 for a linear progress <paramref name="t"/> 0..1. Values outside the
    /// range clamp: an animation cannot be less begun than begun, and time past the end is the end.
    /// </summary>
    public static float Ease(this Curve curve, float t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;

        // The LINEAR curve is worth its own branch: it is the identity, and solving for it wastes
        // iterations to arrive back at t.
        if (curve.X1 == curve.Y1 && curve.X2 == curve.Y2) return t;

        return Bezier(SolveForX(curve, t), curve.Y1, curve.Y2);
    }

    /// <summary>The parameter s where the curve's x reaches <paramref name="x"/>.</summary>
    private static float SolveForX(Curve curve, float x)
    {
        var s = x;   // x is a good first guess: the curve is monotonic in time by construction.

        for (var i = 0; i < NewtonIterations; i++)
        {
            var slope = BezierSlope(s, curve.X1, curve.X2);
            if (MathF.Abs(slope) < NewtonMinSlope) break;   // flat here — Newton would leap away
            var error = Bezier(s, curve.X1, curve.X2) - x;
            if (MathF.Abs(error) < Precision) return s;
            s -= error / slope;
        }

        // Bisection finishes what Newton could not: slower, and it cannot diverge.
        float low = 0, high = 1;
        s = Math.Clamp(s, low, high);
        for (var i = 0; i < BisectionIterations; i++)
        {
            var value = Bezier(s, curve.X1, curve.X2);
            if (MathF.Abs(value - x) < Precision) return s;
            if (value < x) low = s; else high = s;
            s = (low + high) / 2;
        }
        return s;
    }

    /// <summary>A cubic bézier with endpoints pinned at 0 and 1, in one dimension.</summary>
    private static float Bezier(float s, float a, float b)
    {
        var inverse = 1 - s;
        return (3 * inverse * inverse * s * a) + (3 * inverse * s * s * b) + (s * s * s);
    }

    private static float BezierSlope(float s, float a, float b)
    {
        var inverse = 1 - s;
        return (3 * inverse * inverse * a)
            + (6 * inverse * s * (b - a))
            + (3 * s * s * (1 - b));
    }
}
