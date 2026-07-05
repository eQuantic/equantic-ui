// The NORMATIVE Photon shader source (plan D3): ONE Slang file, compiled OFFLINE per backend —
// MSL for Metal, SPIR-V for Vulkan (scripts/generate-shaders.sh; generated outputs are committed
// and never hand-edited). This is a transliteration of eQuantic.UI.Native.Engine/Sdf.cs — the
// engine's normative math — plus Paint.ColorAt (sRGB-space gradient lerp), ColorSpace
// (IEC 61966-2-1 sRGB→linear) and the baked device-space rrect clip. Change Sdf.cs → change this
// file → regenerate → the golden/parity suites judge.

struct DrawUniforms
{
    float4 inv0;      // inverse transform: m11, m21, m31, deviceScale
    float4 inv1;      // inverse transform: m12, m22, m32, strokeWidth
    float4 rect;      // center.x, center.y, halfSize.w, halfSize.h
    float4 radii;     // tl, tr, br, bl (normalized)
    float4 colorA;    // sRGB 0..1 straight alpha (solid / gradient start)
    float4 colorB;    // sRGB 0..1 straight alpha (gradient end)
    float4 gradient;  // start.x, start.y, end.x, end.y (LOCAL space)
    float4 flags;     // x: 0 fill | 1 stroke | 2 shadow · y: 0 solid | 1 linear gradient · z: 1 = clipped
    float4 clipRect;  // DEVICE-space clip: center.x, center.y, halfSize.w, halfSize.h
    float4 clipRadii; // tl, tr, br, bl
};

ConstantBuffer<DrawUniforms> u;

// ColorSpace.SrgbToLinear — the exact IEC 61966-2-1 piecewise curve.
float srgbToLinear(float c)
{
    return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
}

// Sdf.RoundedRect — quadrant radius select (y grows down), q = |p| − (half − r).
float sdRoundedRect(float2 p, float2 halfSize, float4 radii)
{
    float r = p.x >= 0.0
        ? (p.y >= 0.0 ? radii.z : radii.y)   // BR : TR
        : (p.y >= 0.0 ? radii.w : radii.x);  // BL : TL
    float2 q = abs(p) - (halfSize - r);
    float outside = length(max(q, 0.0));
    float inside = min(max(q.x, q.y), 0.0);
    return outside + inside - r;
}

[shader("vertex")]
float4 fullscreen_vertex(uint vid: SV_VertexID) : SV_Position
{
    // Fullscreen triangle, no vertex buffer.
    float2 positions[3] = { float2(-1.0, -1.0), float2(3.0, -1.0), float2(-1.0, 3.0) };
    return float4(positions[vid], 0.0, 1.0);
}

[shader("fragment")]
float4 sdf_fragment(float4 position: SV_Position) : SV_Target
{
    // position.xy is the pixel CENTER in device space — the Reference's sampling point.
    float2 pixel = position.xy;
    float2 local = float2(
        pixel.x * u.inv0.x + pixel.y * u.inv0.y + u.inv0.z,
        pixel.x * u.inv1.x + pixel.y * u.inv1.y + u.inv1.z);

    float d = sdRoundedRect(local - u.rect.xy, u.rect.zw, u.radii);
    if (u.flags.x > 0.5 && u.flags.x < 1.5)
        d = abs(d) - u.inv1.w / 2.0;  // Sdf.Stroke — centered band

    float coverage;
    if (u.flags.x > 1.5)
    {
        // Sdf.ShadowCoverage (§05): 1 − smoothstep(−1.5σ, +1.5σ, d), σ = blur/2 — device-scaled.
        float sigma = u.inv1.w * u.inv0.w / 2.0;
        if (sigma <= 0.0)
        {
            coverage = clamp(0.5 - d * u.inv0.w, 0.0, 1.0);
        }
        else
        {
            float t = clamp((d * u.inv0.w + 1.5 * sigma) / (3.0 * sigma), 0.0, 1.0);
            coverage = 1.0 - t * t * (3.0 - 2.0 * t);
        }
    }
    else
    {
        coverage = clamp(0.5 - d * u.inv0.w, 0.0, 1.0);  // Sdf.Coverage, device-scaled
    }
    if (coverage <= 0.0) discard;

    // Baked clip: multiply by the clip rrect's coverage in DEVICE space (scale 1) — the exact
    // Reference math, so clip edges anti-alias identically on every rasterizer.
    if (u.flags.z > 0.5)
    {
        float cd = sdRoundedRect(pixel - u.clipRect.xy, u.clipRect.zw, u.clipRadii);
        coverage *= clamp(0.5 - cd, 0.0, 1.0);
        if (coverage <= 0.0) discard;
    }

    // Paint.ColorAt — gradients interpolate per-channel in sRGB space (the CSS/Skia look).
    float4 srgb = u.colorA;
    if (u.flags.y > 0.5)
    {
        float2 axis = u.gradient.zw - u.gradient.xy;
        float len2 = dot(axis, axis);
        float t = len2 <= 0.0 ? 0.0 : clamp(dot(local - u.gradient.xy, axis) / len2, 0.0, 1.0);
        srgb = lerp(u.colorA, u.colorB, t);
    }

    // ColorSpace.ToPremultipliedLinear(color, coverage): linearize, premultiply by alpha·coverage.
    float a = srgb.a * coverage;
    float3 lin = float3(srgbToLinear(srgb.r), srgbToLinear(srgb.g), srgbToLinear(srgb.b)) * a;
    return float4(lin, a);  // premultiplied linear; the sRGB target encodes on store
}
