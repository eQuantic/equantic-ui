namespace eQuantic.UI.Primitives;

/// <summary>
/// One stop of a <see cref="Mask"/> (#1938): the opacity the subtree keeps at a fraction of the
/// gradient's ray. The offset is 0 where the ray starts and 1 where it ends, and it may lie outside
/// that range, as a CSS color stop's may. The opacity is 0 (nothing shows) to 1 (all of it shows).
/// <para>
/// A stop alone refuses nothing: a mask's constructor checks every stop it is given and names its own
/// <c>stops</c> parameter, since the mask is what an app writes.
/// </para>
/// </summary>
/// <param name="Offset">The position along the ray: a finite number, 0 at its start and 1 at its end.</param>
/// <param name="Opacity">What the subtree keeps there, from 0 to 1.</param>
[ZeroConstructs("vocabulary.ts: `constructor(offset = 0, opacity = 0)`.")]
public readonly record struct MaskStop(float Offset, float Opacity);
