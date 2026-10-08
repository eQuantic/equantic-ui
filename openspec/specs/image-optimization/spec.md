# image-optimization Specification

## Purpose
What the `/_equantic/image` endpoint of `eQuantic.UI.Images` reads, refuses and answers, so an
image under `wwwroot` is served resized, in a format the browser takes, as it is displayed.

## Requirements

### Requirement: The optimizer reads the web's formats and nothing else

The image optimizer SHALL decode a source only when its bytes are a JPEG, PNG, GIF, WebP or BMP, and
SHALL refuse any other format before it decodes a pixel. The `/_equantic/image` endpoint SHALL
refuse a file whose extension is not one of those formats', and SHALL answer 400 for a source whose
bytes the optimizer refuses.

#### Scenario: A TIFF under wwwroot

- **WHEN** `/_equantic/image?url=/images/scan.tiff&w=640` is requested
- **THEN** the answer is 400, and the file is not opened

#### Scenario: Text under an image's name

- **WHEN** `/_equantic/image?url=/images/not-an-image.jpg&w=640` is requested and the file holds text
- **THEN** the answer is 400 naming the formats read, where it was 500

### Requirement: A source past the pixel ceiling is refused from its header

The optimizer SHALL refuse a source whose header declares more than 268,402,689 pixels
(16,383 × 16,383) before it allocates a pixel, and the endpoint SHALL answer 400 for it.

#### Scenario: A PNG header asking for 400 megapixels

- **WHEN** a PNG whose header declares 20,000 × 20,000 pixels is optimized
- **THEN** an `InvalidDataException` names its 20000 × 20000 pixels, and the endpoint answers 400

### Requirement: An image is optimized as it is displayed

The optimizer SHALL apply a source's EXIF orientation to its pixels before it resizes them, SHALL
take the requested width as the displayed width, and `GetDimensionsAsync` SHALL answer the displayed
size. The result SHALL hold sRGB pixels and no colour profile.

#### Scenario: A photo taken with the camera held upright

- **WHEN** a 40 × 20 JPEG, red on its left half, with EXIF orientation 6, is optimized to PNG at width 640
- **THEN** the result is 20 × 40, red on its top half

#### Scenario: The size as displayed

- **WHEN** `GetDimensionsAsync` reads a 1920 × 1080 JPEG with EXIF orientation 6
- **THEN** it answers 1080 × 1920

#### Scenario: No profile in the result

- **WHEN** an image is optimized to JPEG or WebP
- **THEN** the result holds no ICC profile

### Requirement: An animated source is served as it is

The optimizer SHALL hand an animated source, one of more than one frame, back as it is, and the
endpoint SHALL serve it with its own content type.

#### Scenario: An animated GIF asked for as WebP

- **WHEN** `/_equantic/image?url=/images/animated.gif&w=640` is requested with `Accept: image/webp`
- **THEN** the answer is the GIF's own bytes, with `Content-Type: image/gif`

### Requirement: Formats takes only what an encoder writes

`ImageOptimizationOptions.Formats` SHALL accept `image/webp`, `image/png` and `image/jpeg`, and its
validation SHALL refuse any other, `image/avif` included, so an app that asks for a format no encoder
writes stops at startup. The endpoint SHALL label a response with the content type of its bytes.

#### Scenario: AVIF listed first

- **WHEN** the optimizer is configured with `Formats = ["image/avif", "image/webp"]`
- **THEN** validation throws an `ArgumentException` naming `image/avif`, where every browser that takes AVIF was answered with a JPEG labelled `image/avif`

### Requirement: The optimizer builds without a license key

The image optimizer SHALL depend on no package that needs a license key to build or to run, and an
app on Linux, macOS or Windows SHALL get the natives it needs from the package itself.

#### Scenario: A Release build

- **WHEN** `eQuantic.UI.Images` is packed in Release with no key in the environment
- **THEN** it builds, with no audit warning
