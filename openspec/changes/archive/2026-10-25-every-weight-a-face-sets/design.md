## Context

A weight travels as `FontWeight` everywhere in C#, and every C# realizer turns it into its number with a
cast: the server's stylesheet and markup, the email realizer, DirectWrite on Windows. Two places read it
otherwise:

- **The runtime.** The transpiler emits an enum member as its camelCase name (`'semiBold'`), so the
  runtime turns a weight into a number in one function, `cssFontWeight`, through a switch written by
  hand. A member it does not list falls to its default, 400.
- **Photon on Apple and Android.** CoreText and Android are asked for a single trait, bold, at 600 and
  above (`weight >= FontWeight.SemiBold`). The shell names the family, and the platform hands back its
  Regular or its Bold face. The CoreText service's own summary calls this a v1 fence.

## Goals / Non-Goals

**Goals:** the vocabulary names the nine weights OpenType defines, and text set in any of them is drawn at
that weight on the web and on every Photon shell that can draw it.

**Non-Goals:** a variable face's weight axis on Android and Windows, which is #1061. On Apple it is a goal
after all: see the decision below. Windows choosing a shipped file by the catalog's weights, which is #1099. Weights between the nine (`(FontWeight)450`): the
vocabulary names the nine, as CSS's keywords and Flutter's `FontWeight` do.

## Decisions

- **How Flutter does it.** `FontWeight.w100` to `FontWeight.w900`, named `thin` to `black`, and the engine
  asks the platform for the number. Here the names are the vocabulary's (`Thin`, `ExtraLight`, `Light`,
  `Regular`, `Medium`, `SemiBold`, `Bold`, `ExtraBold`, `Black`, the OpenType names) and each shell asks
  its engine for the weight in the engine's own scale.
- **Appended, so no value moves.** C# bakes an enum constant into the consuming assembly, so the members
  are appended and `EnumValueAbiTests` holds every value where it was.
- **The runtime's number is generated.** The switch was a hand mirror of the enum, and it had already
  fallen behind the vocabulary it mirrored the day a member was added. `EnumUnionsTsGenerator` writes a
  table of each weight's name and number beside the union (`FontWeightNumbers`), from the enum itself, and
  `cssFontWeight` reads it. A weight is the one vocabulary enum whose value is its meaning: CSS, CoreText,
  DirectWrite and Android all take the number. So the table is written for it alone, not for every enum.
- **CoreText is asked for the weight.** The descriptor carries the weight trait (`kCTFontWeightTrait`, in
  `kCTFontTraitsAttribute`) in Apple's scale, the values of `NSFontWeight`: Thin is Apple's ultraLight
  (−0.8), ExtraLight its thin (−0.6), Light −0.4, Regular 0, Medium 0.23, SemiBold 0.3, Bold 0.4,
  ExtraBold its heavy (0.56) and Black 0.62, read off a weight's number, interpolated between them, so no
  weight falls to a default. For a family the machine has, one that ships no files, the matcher answers the
  family's face nearest that weight. For the system face, the platform's own UI or monospaced font, a copy
  of it with the weight. The italic is asked in the same traits, as the symbolic trait it is, so a weight and
  a slant are one question. The font cache is keyed by the weight, where it was keyed by bold.
- **A shipped family is drawn from the file of its weight, opened directly.** Measured on the probe, CoreText's
  match by family and weight trait picked a neighbouring file of Inter for Thin, Light and Black: the engine
  reads a face's weight from its file, and the files do not all say it the same way. So the shell picks the
  file itself, by the weights the catalog gives each file, and opens THAT one (`CTFontCreateWithGraphicsFont`
  on the face it registered): every Inter weight then measured on CoreText what it measures in a browser,
  to the tenth of a pixel. The choice is a browser's (CSS Fonts §5.2), in one place every shell calls
  (`ShippedFaces.FileFor`): a file whose weights hold the one asked for; failing that, below 400 the nearest
  lighter then the nearest heavier, above 500 the nearest heavier then the nearest lighter, and for 400 to
  500 the heavier up to 500 first. Android already opened a file per weight, with a rule of its own; it
  calls the same one now. A family the machine has, with no files, is still matched by CoreText.
- **On Apple a variable file is opened at the weight on its axis.** Opened as it is, a variable file sets
  every weight at its default instance, and the review of this change measured that as a regression: the
  bold trait laid over a matched face had reached a variable font's Bold instance (Skia's, on macOS), where
  the file opened directly drew it at Regular. So the shell opens a variable file with `kCTFontVariationAttribute`
  setting `wght` at the weight asked, clamped to the file's range, which every weight on the axis then draws at,
  not just the named instances. Android and Windows keep #1061.
- **A shipped file is not registered with the font manager.** Registration existed so CoreText's match by
  family would find a shipped face; nothing matches a shipped family by name now, and a registration the
  font manager refuses, a name the machine already has, would only make a family that draws fall back.
  The shell reads each file into a graphics font, keeps it, and opens fonts from it.
- **A static face's weight is the one its `STAT` table gives it.** Google's static Thin and ExtraLight of Inter
  both carry the weight class 250, the value Windows's GDI needs not to embolden a thin face, and the catalog,
  reading the weight class, saw one face twice and stopped the build (EQ4006). Their `STAT` tables give each
  one position on `wght`, 100 and 200, which is the face's own weight. A `STAT` with several positions, a
  range or a position shared with other axes describes a family rather than the face, and the weight class
  stands then; so it does for a face with no `STAT`. A position flagged as an older sibling's is another
  font's, and is passed over.
- **Android is asked for the weight where it can be.** `Typeface.create(Typeface, int, boolean)` takes a
  weight from API 28. The shell supports API 26, so below 28 it keeps the style it had, regular and bold,
  which is the most those versions can be asked for.
- **Windows and the web need nothing.** DirectWrite and every C# realizer already take the number.

## Risks / Trade-offs

- On Apple and on Android, `Medium` and `SemiBold` text draws at its own weight now. A screen that relied
  on `SemiBold` drawing as `Bold` there looks lighter, as it already does on the web and on Windows: the
  targets now agree.
- A family that ships fewer weights than are asked for draws from the file a browser would choose, at that
  file's weight: no shell synthesises a weight a family does not ship, except Android's own bold above 600
  when the nearest file is lighter, as a browser does.
- Windows asks DirectWrite for a shipped family by name and weight, and DirectWrite reads a face's weight from
  its file, so two of a family's files that carry the same weight class, as Google's Thin and ExtraLight do,
  are one weight there. It is not measured here, since this machine runs no Windows; the shell is untouched,
  and #1099 routes it through `ShippedFaces.FileFor`.
