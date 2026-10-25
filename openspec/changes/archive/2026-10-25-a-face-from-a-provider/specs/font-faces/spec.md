## ADDED Requirements

### Requirement: A face from a provider is fetched by the build and served by the app

A family an app declares from Google Fonts with `[assembly: GoogleFont(family, weights…)]`, the italic cut
of each weight included when `Italic` is true, SHALL be fetched by the build before the app compiles,
embedded in the app's assembly and carried by the app's font catalog as a face under `Assets` is, so every
target loads it from the app. The build SHALL keep the files out of the source tree and SHALL fetch each
file once. The SDK SHALL pin each file, the first time it fetches it, in `fonts.lock.json` beside the
project, by its URL and the SHA-256 of its bytes, and SHALL fetch a pinned file from its pinned URL only.
The build SHALL fail when a pinned file's bytes differ from its pin, when its URL no longer answers, and
when a cut is added to a family whose pinned cuts, whichever build declared them, the provider has since
replaced or no longer has, until the family's pins
are taken out of the lock, after which the next build SHALL pin the provider's current files. No build SHALL
take a pin out of the lock, since the families a build reads depend on its symbols, and each build SHALL embed
the files of the families it declares and no others. A family,
weight or italic the provider does not have SHALL fail the build, naming it, and so SHALL a declaration
whose family or weights are not literals. No page SHALL ask the provider for anything.

#### Scenario: A family declared in C#

- **WHEN** an app declares `[assembly: GoogleFont("Inter", FontWeight.Regular, FontWeight.Bold, Italic = true)]`
- **THEN** the build fetches four files, embeds each in the app's assembly, `Fonts.Inter` names "Inter"
  with the four of them, `fonts.lock.json` pins each, and a page that sets text in it loads the files from
  the app and from nowhere else

#### Scenario: A rebuild

- **WHEN** the app is built again with its files fetched
- **THEN** nothing is asked of the provider

#### Scenario: A family another build declares

- **WHEN** a Release build does not see a family declared under `#if DEBUG`
- **THEN** the family's pins stay in the lock, the Release build embeds none of its files, and the next
  Debug build asks the provider for nothing

#### Scenario: A provider that changed a pinned file

- **WHEN** a pinned file's bytes no longer match its pin
- **THEN** the build fails, naming the file and the way to renew it, and once the family is taken out of
  the lock the next build pins the provider's current files

#### Scenario: A face the provider does not have

- **WHEN** an app declares `[assembly: GoogleFont("Bebas Neue", FontWeight.Bold)]`
- **THEN** the build fails, saying that Google Fonts has no Bold of "Bebas Neue"

#### Scenario: A declaration the build cannot read

- **WHEN** a `GoogleFont`'s family is a constant declared elsewhere in the app
- **THEN** the build fails at the attribute, saying that the family has to be a literal

#### Scenario: A Photon app

- **WHEN** a Photon app declares a family from Google Fonts
- **THEN** its own assembly holds the family's files, and the shell registers them as it registers a face
  under `Assets`
