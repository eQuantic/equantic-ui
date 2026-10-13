#!/usr/bin/env python3
"""The README's pictures, made from the raw captures scripts/capture-readme.sh takes (#880).

    readme-captures.py check <picture> [...]      fail when a capture drew nothing
    readme-captures.py compose <raw dir> <out dir>  write the pictures the README shows

A capture can be a picture of nothing: the first launch of an app after its install is slow, and a
simulator photographed before the first frame gives a black screen with a status bar on top. Two of
the first six iOS captures were that. So `check` counts the colours under the status bar and refuses
a picture that has almost none, and `compose` checks every capture it reads.

A still is scaled to twice the width the README draws it at. A phone's and a window's picture get
round corners on a transparent ground, so they sit on GitHub's light page and its dark one alike. The
tour is ten pages of the dashboard on ONE palette, so its frames do not flicker and the file stays
small.

Needs Pillow (`python3 -m pip install pillow`), and says so when it is not there.
"""
import os
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("readme-captures.py needs Pillow: python3 -m pip install pillow")

# A drawn screen holds hundreds of colours under its status bar. A black one holds one or two.
FEWEST_COLOURS = 40

STILLS = (
    ("web-dark-charts", "web-charts.png", 1200),
    ("web-dark-diff", "web-code-diff.png", 1200),
    ("web-light-form", "web-forms.png", 1200),
)
TOUR = ("payments", "charts", "sheet", "code", "diff", "markdown", "form", "rows", "i18n", "colors")
TOUR_SIZE = (1200, 750)
WINDOWS = (
    ("desktop-dark-charts", "desktop-gallery.png"),
    ("desktop-light-buttons", "desktop-gallery-light.png"),
)
PHONES = (
    ("ios-dark-home", "ios-home.png"),
    ("ios-light-detail", "ios-transfer.png"),
    ("android-dark-transactions", "android-transactions.png"),
    ("android-dark-cards", "android-cards.png"),
)


def colours_drawn(image):
    """How many colours a picture holds under its top twelfth, where a status bar is."""
    body = image.convert("RGB").crop((0, image.height // 12, image.width, image.height)).resize((80, 160))
    return len(body.getcolors(80 * 160))


def checked(path):
    if not os.path.isfile(path):
        sys.exit(f"{path}: no capture there. Take it with scripts/capture-readme.sh")
    image = Image.open(path)
    drawn = colours_drawn(image)
    if drawn < FEWEST_COLOURS:
        sys.exit(f"{path}: a picture of nothing ({drawn} colours under its status bar). Take it again")
    return image.convert("RGB")


def scaled(image, width):
    return image.resize((width, round(image.height * width / image.width)), Image.LANCZOS)


def rounded(image, radius):
    # Drawn four times as large and scaled down, so the corner is smooth.
    mask = Image.new("L", (image.width * 4, image.height * 4), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, mask.width - 1, mask.height - 1), radius * 4, fill=255)
    result = image.convert("RGBA")
    result.putalpha(mask.resize(image.size, Image.LANCZOS))
    return result


def compose(raw, out):
    os.makedirs(out, exist_ok=True)

    def capture(name):
        return checked(os.path.join(raw, name + ".png"))

    def save(image, name):
        path = os.path.join(out, name)
        image.save(path, optimize=True)
        print(f"{name}: {image.width}x{image.height}, {os.path.getsize(path) // 1024} KB")

    for source, name, width in STILLS:
        save(scaled(capture(source), width), name)

    width, height = TOUR_SIZE
    frames = [capture("web-dark-" + page).resize(TOUR_SIZE, Image.LANCZOS) for page in TOUR]
    mosaic = Image.new("RGB", (width * 5, height * 2))
    for index, frame in enumerate(frames):
        mosaic.paste(frame, ((index % 5) * width, (index // 5) * height))
    palette = mosaic.quantize(colors=255, method=Image.MEDIANCUT, dither=Image.NONE)
    pages = [frame.quantize(palette=palette, dither=Image.NONE) for frame in frames]
    path = os.path.join(out, "web-tour.gif")
    pages[0].save(path, save_all=True, append_images=pages[1:], duration=1600, loop=0, optimize=True, disposal=1)
    print(f"web-tour.gif: {width}x{height}, {len(pages)} pages, {os.path.getsize(path) // 1024} KB")

    for source, name in WINDOWS:
        save(rounded(capture(source), 10), name)
    for source, name in PHONES:
        save(rounded(scaled(capture(source), 540), 44), name)


def main(arguments):
    if len(arguments) >= 2 and arguments[0] == "check":
        for path in arguments[1:]:
            checked(path)
    elif len(arguments) == 3 and arguments[0] == "compose":
        compose(arguments[1], arguments[2])
    else:
        sys.exit(__doc__.strip().split("\n\n")[1])


if __name__ == "__main__":
    main(sys.argv[1:])
