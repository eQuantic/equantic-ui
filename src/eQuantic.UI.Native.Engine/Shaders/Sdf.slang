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

[[vk::binding(0)]] ConstantBuffer<DrawUniforms> u;

// W4b: the A8 coverage raster a Texture command samples (glyph blocks; images later). Bound only
// by the textured pipeline; the SDF fragment never references it.
[[vk::binding(1)]] Texture2D<float> coverageTexture;

// W4 images: straight-alpha color texture (sRGB view — samples arrive linear).
[[vk::binding(2)]] Texture2D<float4> colorTexture;

// D6b: the engine's FIRST sampler — declared after the textures so its binding lands at 3 on the
// SPIR-V side (declaration order assigns bindings). Only the FILTERED paths use it (the blur
// chain here; scaled images later): glyph/icon/image rasters keep texel-exact Load, where nearest
// is correct rather than a compromise. The backend binds nearest or linear per draw.
[[vk::binding(3)]] SamplerState textureSampler;

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
    // flags.y: 0 solid · 1 linear (axis in `gradient`) · 2 RADIAL (center.xy + radii.zw, same slots).
    float4 srgb = u.colorA;
    if (u.flags.y > 1.5)
    {
        // Elliptical radial: normalizing by each radius maps the ellipse to a unit circle.
        float dx = u.gradient.z <= 0.0 ? 0.0 : (local.x - u.gradient.x) / u.gradient.z;
        float dy = u.gradient.w <= 0.0 ? 0.0 : (local.y - u.gradient.y) / u.gradient.w;
        float t = clamp(sqrt(dx * dx + dy * dy), 0.0, 1.0);
        srgb = lerp(u.colorA, u.colorB, t);
    }
    else if (u.flags.y > 0.5)
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

// W4b — DrawCommandKind.Texture: A8 coverage × tint, NEAREST via texel Load (rasters are
// device-scale, texels map 1:1 — no sampler, no filtering, exact parity with the Reference).
// Uniform reuse: rect packs the draw rect (center/half); gradient.xy carries the texture size.
[shader("fragment")]
float4 textured_fragment(float4 position: SV_Position) : SV_Target
{
    float2 pixel = position.xy;
    float2 local = float2(
        pixel.x * u.inv0.x + pixel.y * u.inv0.y + u.inv0.z,
        pixel.x * u.inv1.x + pixel.y * u.inv1.y + u.inv1.z);

    // UV inside the draw rect — outside is empty (the Reference's early-out).
    float2 rectMin = u.rect.xy - u.rect.zw;
    float2 uv = (local - rectMin) / (u.rect.zw * 2.0);
    if (uv.x < 0.0 || uv.x >= 1.0 || uv.y < 0.0 || uv.y >= 1.0) discard;

    int tx = min(int(u.gradient.x) - 1, int(uv.x * u.gradient.x));
    int ty = min(int(u.gradient.y) - 1, int(uv.y * u.gradient.y));
    float coverage = coverageTexture.Load(int3(tx, ty, 0));
    if (coverage <= 0.0) discard;

    if (u.flags.z > 0.5)
    {
        float cd = sdRoundedRect(pixel - u.clipRect.xy, u.clipRect.zw, u.clipRadii);
        coverage *= clamp(0.5 - cd, 0.0, 1.0);
        if (coverage <= 0.0) discard;
    }

    // GRADIENT TINT (gradient text, and artwork's own runs): the geometry rides `radii` and the
    // second stop `colorB` — both slots the textured path never used, so this needed no uniform
    // re-layout. `flags.y` says WHICH run: 1 linear, 2 elliptical radial, the same two the SDF
    // path draws. The interpolation is the SAME sRGB-space lerp Paint.ColorAt performs, so a run
    // over glyph coverage, a run over artwork and a run over an rrect fill cannot disagree.
    float4 srgb = u.colorA;
    if (u.flags.y > 1.5)
    {
        // radii.xy = centre, radii.zw = the (x, y) radii — normalized elliptical distance.
        float dx = u.radii.z <= 0.0 ? 0.0 : (local.x - u.radii.x) / u.radii.z;
        float dy = u.radii.w <= 0.0 ? 0.0 : (local.y - u.radii.y) / u.radii.w;
        float t = clamp(sqrt(dx * dx + dy * dy), 0.0, 1.0);
        srgb = lerp(u.colorA, u.colorB, t);
    }
    else if (u.flags.y > 0.5)
    {
        float2 axis = u.radii.zw - u.radii.xy;
        float len2 = dot(axis, axis);
        float t = len2 <= 0.0 ? 0.0 : clamp(dot(local - u.radii.xy, axis) / len2, 0.0, 1.0);
        srgb = lerp(u.colorA, u.colorB, t);
    }

    float a = srgb.a * coverage;
    float3 lin = float3(srgbToLinear(srgb.r), srgbToLinear(srgb.g), srgbToLinear(srgb.b)) * a;
    return float4(lin, a);
}

// DUAL-KAWASE BLUR (plan W3/D6b) — the two halves of the pyramid, each one fullscreen draw over
// the DESTINATION with the source bound at binding 2 and the LINEAR sampler at binding 3. The tap
// pattern transliterates Blur.cs (the normative math): 5 taps averaged /8 going down, 8 taps /12
// coming up, offsets of half a destination texel carried in `gradient.xy` so the shader never
// divides. Samples arrive LINEAR (the _sRGB view decodes before filtering — required by spec) and
// premultiplied, exactly like the CPU twin.
[shader("fragment")]
float4 blur_down(float4 position: SV_Position) : SV_Target
{
    float2 uv = position.xy / (u.rect.zw * 2.0);
    float2 half_ = u.gradient.xy;

    float4 sum = colorTexture.Sample(textureSampler, uv) * 4.0;
    sum += colorTexture.Sample(textureSampler, uv - half_);
    sum += colorTexture.Sample(textureSampler, uv + half_);
    sum += colorTexture.Sample(textureSampler, uv + float2(half_.x, -half_.y));
    sum += colorTexture.Sample(textureSampler, uv - float2(half_.x, -half_.y));
    return sum / 8.0;
}

[shader("fragment")]
float4 blur_up(float4 position: SV_Position) : SV_Target
{
    float2 uv = position.xy / (u.rect.zw * 2.0);
    float2 half_ = u.gradient.xy;

    float4 sum = colorTexture.Sample(textureSampler, uv + float2(-half_.x * 2.0, 0.0));
    sum += colorTexture.Sample(textureSampler, uv + float2(-half_.x, half_.y)) * 2.0;
    sum += colorTexture.Sample(textureSampler, uv + float2(0.0, half_.y * 2.0));
    sum += colorTexture.Sample(textureSampler, uv + float2(half_.x, half_.y)) * 2.0;
    sum += colorTexture.Sample(textureSampler, uv + float2(half_.x * 2.0, 0.0));
    sum += colorTexture.Sample(textureSampler, uv + float2(half_.x, -half_.y)) * 2.0;
    sum += colorTexture.Sample(textureSampler, uv + float2(0.0, -half_.y * 2.0));
    sum += colorTexture.Sample(textureSampler, uv + float2(-half_.x, -half_.y)) * 2.0;
    return sum / 12.0;
}

// GROUP-OPACITY LAYER COMPOSITE (spec S1): draws an offscreen layer over the surface below at the
// layer's alpha. The source is a RENDER TARGET, so its texels are already PREMULTIPLIED (and the
// _sRGB view hands them back linear) — compositing at alpha is therefore a plain scale of the
// premultiplied value, NOT the straight-alpha math textured_rgba_fragment does. That difference is
// exactly why this needs its own entry point instead of reusing the image path.
// Uniform reuse: colorA.a carries the layer alpha; gradient.xy the texture size.
[shader("fragment")]
float4 layer_composite(float4 position: SV_Position) : SV_Target
{
    float2 pixel = position.xy;
    float2 local = float2(
        pixel.x * u.inv0.x + pixel.y * u.inv0.y + u.inv0.z,
        pixel.x * u.inv1.x + pixel.y * u.inv1.y + u.inv1.z);

    float2 rectMin = u.rect.xy - u.rect.zw;
    float2 uv = (local - rectMin) / (u.rect.zw * 2.0);
    if (uv.x < 0.0 || uv.x >= 1.0 || uv.y < 0.0 || uv.y >= 1.0) discard;

    int tx = min(int(u.gradient.x) - 1, int(uv.x * u.gradient.x));
    int ty = min(int(u.gradient.y) - 1, int(uv.y * u.gradient.y));
    float4 texel = colorTexture.Load(int3(tx, ty, 0));

    float alpha = u.colorA.a;
    if (u.flags.z > 0.5)
    {
        float cd = sdRoundedRect(pixel - u.clipRect.xy, u.clipRect.zw, u.clipRadii);
        alpha *= clamp(0.5 - cd, 0.0, 1.0);
    }
    if (alpha <= 0.0) discard;

    return texel * alpha;
}

// W4 images — DrawCommandKind.Texture with an Rgba8 texture: the texel IS the color (the paint
// tint is ignored); NEAREST via Load like the coverage path. The _sRGB texture view hands the
// sample back LINEAR; we premultiply by its alpha (× clip coverage) for the src-over blend.
[shader("fragment")]
float4 textured_rgba_fragment(float4 position: SV_Position) : SV_Target
{
    float2 pixel = position.xy;
    float2 local = float2(
        pixel.x * u.inv0.x + pixel.y * u.inv0.y + u.inv0.z,
        pixel.x * u.inv1.x + pixel.y * u.inv1.y + u.inv1.z);

    float2 rectMin = u.rect.xy - u.rect.zw;
    float2 uv = (local - rectMin) / (u.rect.zw * 2.0);
    if (uv.x < 0.0 || uv.x >= 1.0 || uv.y < 0.0 || uv.y >= 1.0) discard;

    int tx = min(int(u.gradient.x) - 1, int(uv.x * u.gradient.x));
    int ty = min(int(u.gradient.y) - 1, int(uv.y * u.gradient.y));
    float4 texel = colorTexture.Load(int3(tx, ty, 0));

    float coverage = 1.0;
    if (u.flags.z > 0.5)
    {
        float cd = sdRoundedRect(pixel - u.clipRect.xy, u.clipRect.zw, u.clipRadii);
        coverage = clamp(0.5 - cd, 0.0, 1.0);
    }

    float a = texel.a * coverage;
    if (a <= 0.0) discard;
    return float4(texel.rgb * a, a);
}
