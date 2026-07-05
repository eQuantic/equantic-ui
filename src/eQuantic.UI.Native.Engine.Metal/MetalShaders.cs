namespace eQuantic.UI.Native.Engine.Metal;

/// <summary>
/// The spike's MSL source — a TRANSLITERATION of the engine's normative math: <c>Sdf.cs</c>
/// (per-corner rrect, centered stroke, 1px coverage ramp), <c>Paint.ColorAt</c> (sRGB-space gradient
/// interpolation) and <c>ColorSpace</c> (IEC sRGB→linear; premultiplied output — the sRGB render
/// target converts on store, exactly the Reference surface model). Runtime-compiled for the spike;
/// the production path precompiles via the Slang toolchain (plan D3) — creating pipelines at draw
/// time stays a bug by definition (D5); here the ONE pipeline is built once per device.
/// </summary>
internal static class MetalShaders
{
    public const string Source = """
#include <metal_stdlib>
using namespace metal;

struct DrawUniforms {
    float4 inv0;      // inverse transform: m11, m21, m31, deviceScale
    float4 inv1;      // inverse transform: m12, m22, m32, strokeWidth
    float4 rect;      // center.x, center.y, halfSize.w, halfSize.h
    float4 radii;     // tl, tr, br, bl (normalized)
    float4 colorA;    // sRGB 0..1 straight alpha (solid / gradient start)
    float4 colorB;    // sRGB 0..1 straight alpha (gradient end)
    float4 gradient;  // start.x, start.y, end.x, end.y (LOCAL space)
    float4 flags;     // x: 0 fill | 1 stroke · y: 0 solid | 1 linear gradient · z: 1 = clipped
    float4 clipRect;  // DEVICE-space clip: center.x, center.y, halfSize.w, halfSize.h
    float4 clipRadii; // tl, tr, br, bl
};

vertex float4 fullscreen_vertex(uint vid [[vertex_id]]) {
    // Fullscreen triangle, no vertex buffer.
    float2 positions[3] = { float2(-1.0, -1.0), float2(3.0, -1.0), float2(-1.0, 3.0) };
    return float4(positions[vid], 0.0, 1.0);
}

// ColorSpace.SrgbToLinear — the exact IEC 61966-2-1 piecewise curve.
static float srgb_to_linear(float c) {
    return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
}

// Sdf.RoundedRect — quadrant radius select (y grows down), q = |p| − (half − r).
static float sd_rounded_rect(float2 p, float2 half_size, float4 radii) {
    float r = p.x >= 0.0
        ? (p.y >= 0.0 ? radii.z : radii.y)   // BR : TR
        : (p.y >= 0.0 ? radii.w : radii.x);  // BL : TL
    float2 q = abs(p) - (half_size - r);
    float outside = length(max(q, 0.0));
    float inside = min(max(q.x, q.y), 0.0);
    return outside + inside - r;
}

fragment float4 sdf_fragment(float4 position [[position]],
                             constant DrawUniforms& u [[buffer(0)]]) {
    // position.xy is the pixel CENTER in device space — same sampling point as the Reference.
    // ("device" is an MSL address-space keyword, hence "pixel".)
    float2 pixel = position.xy;
    float2 local = float2(
        pixel.x * u.inv0.x + pixel.y * u.inv0.y + u.inv0.z,
        pixel.x * u.inv1.x + pixel.y * u.inv1.y + u.inv1.z);

    float d = sd_rounded_rect(local - u.rect.xy, u.rect.zw, u.radii);
    if (u.flags.x > 0.5) {
        d = abs(d) - u.inv1.w / 2.0;  // Sdf.Stroke — centered band
    }

    float coverage = clamp(0.5 - d * u.inv0.w, 0.0, 1.0);  // Sdf.Coverage, device-scaled
    if (coverage <= 0.0) discard_fragment();

    // Baked clip: multiply by the clip rrect's coverage in DEVICE space (scale 1) — the exact
    // Reference math, so clip edges anti-alias identically on both rasterizers.
    if (u.flags.z > 0.5) {
        float cd = sd_rounded_rect(pixel - u.clipRect.xy, u.clipRect.zw, u.clipRadii);
        coverage *= clamp(0.5 - cd, 0.0, 1.0);
        if (coverage <= 0.0) discard_fragment();
    }

    // Paint.ColorAt — gradients interpolate per-channel in sRGB space (the CSS/Skia look).
    float4 srgb = u.colorA;
    if (u.flags.y > 0.5) {
        float2 axis = u.gradient.zw - u.gradient.xy;
        float len2 = dot(axis, axis);
        float t = len2 <= 0.0 ? 0.0 : clamp(dot(local - u.gradient.xy, axis) / len2, 0.0, 1.0);
        srgb = mix(u.colorA, u.colorB, t);
    }

    // ColorSpace.ToPremultipliedLinear(color, coverage): linearize, premultiply by alpha·coverage.
    float a = srgb.a * coverage;
    float3 lin = float3(srgb_to_linear(srgb.r), srgb_to_linear(srgb.g), srgb_to_linear(srgb.b)) * a;
    return float4(lin, a);  // premultiplied linear; the sRGB target encodes on store
}
""";
}
