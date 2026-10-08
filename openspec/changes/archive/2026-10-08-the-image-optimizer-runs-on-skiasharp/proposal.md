# Proposal

Closes #710, a sub-issue of #306 (a formal security hardening review).

## Why

Seven GitHub advisories against SixLabors.ImageSharp were published on 2026-10-07, five of them reaching 3.1.12, the version `eQuantic.UI.Images` resolved. NuGet's audit reports them as NU1902 and NU1903, which `TreatWarningsAsErrors` turns into errors, so `build-packages` failed on every pull request, whatever it changed. Two of the five reach the optimizer itself: it decoded every format ImageSharp knows, TIFF included, and parsed ICC profiles. The only patched version, 4.1.2, runs a license check before `CoreCompile` that fails a Release build without a Six Labors key. Edgar decided against a stopgap on 3.1.12 and against a vendor key in every Release build: the optimizer moves to SkiaSharp, which is MIT.

## What Changes

- **The optimizer decodes and encodes with SkiaSharp** 4.153.1. The package references SkiaSharp's Linux natives itself, so an app on a Linux server adds nothing, and nothing needs a license key to build or run.
- **A source is read only in the web's formats**: JPEG, PNG, GIF, WebP and BMP. TIFF goes: the endpoint refuses `.tiff` with 400, and bytes in any other format, whatever their name, are answered 400 where they were answered 500.
- **A source past 268,402,689 pixels (16,383 × 16,383, sharp's default) is refused from its header**, before a pixel is allocated, with 400.
- **The orientation a camera recorded is applied to the pixels.** SkiaSharp's encoders write no EXIF, so an image is turned before it is resized, a requested width is the displayed width, and `GetDimensionsAsync` answers the size as displayed.
- **An animated source is served as it is**, with its own content type, as the Next.js optimizer serves one. No encoder here writes frames, and the first frame alone would stop the animation; ImageSharp re-encoded the frames (measured: a two-frame GIF came back a two-frame WebP).
- **The response's content type is read from its bytes**: a format in `Formats` that no encoder writes (`image/avif`) comes back as a JPEG labelled `image/jpeg`, where it was a JPEG labelled `image/avif`.
- **The pixels are converted to sRGB and written untagged**, which browsers read as sRGB, as sharp writes them. Tagged, every result carried a 472-byte ICC profile, more than an 8-pixel placeholder's whole picture.

For a developer using the SDK: nothing they write changes, and the public surface does not move. A `.tiff` under `wwwroot` is no longer optimized, and an animated GIF comes back as the GIF it was.

## Capabilities

### New Capabilities

- `image-optimization`: what the `/_equantic/image` endpoint reads, refuses and answers.

### Modified Capabilities

None.

## Impact

- `eQuantic.UI.Images`: `ImageOptimizer`, `BlurPlaceholderGenerator` and `ImageOptimizationMiddleware` on SkiaSharp; `SourceImage`, `ImageEncoder` and `ImageFormats` are new and internal. The public surface does not move.
- Tests: `eQuantic.UI.Images.Tests` makes its images with SkiaSharp, and adds orientation, animation, refused formats, the pixel ceiling, the untagged output and the content type read from the bytes.
- The developer surface does not move.
