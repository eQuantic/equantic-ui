# Tasks

## 1. The optimizer runs on SkiaSharp

- [x] 1.1 Measure what ImageSharp 3.1.12 did with an animated source (a two-frame GIF came back a two-frame WebP) and what Skia's encoders write for an 8-pixel image (a 472-byte ICC profile when the pixels are tagged)
- [x] 1.2 `SourceImage` reads the web's five formats and refuses any other, and a source past the pixel ceiling, from its header
- [x] 1.3 The orientation applied to the pixels, the resize halving and then cubic, the pixels in sRGB and written untagged (`ImageEncoder`)
- [x] 1.4 The endpoint: `.tiff` refused, a source it cannot read answered 400, an animated source served as it is, the content type read from the bytes
- [x] 1.5 Prove each in `eQuantic.UI.Images.Tests`, on images SkiaSharp makes and on hand-written ones: an EXIF orientation, an animated GIF, a PNG header asking for 400 megapixels

## 2. Documentation and the suites

- [x] 2.1 The wiki's Image page, English and Portuguese, on the wiki branch of this pull request
- [x] 2.2 `docs/LEDGER.md`: one line citing #710
- [x] 2.3 The Images suite, and CI's `build-packages`, which ImageSharp's advisories failed on every pull request
