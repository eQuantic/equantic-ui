## Context

An app's faces come from one place today: the `.ttf` and `.otf` files under its `Assets` folder, which
the SDK embeds in the app's assembly at evaluation, under `eQuantic.UI.Fonts/`, and which eqicon reads into
the font catalog before the app compiles (`Fonts.g.cs`, #1055). Everything downstream starts from that
catalog: the server serves a catalog's families from the assembly and declares them in the theme's
stylesheet, and Photon's shells register them with their text engines. A face has to be in the assembly
the catalog names, and the catalog has to exist when the app's own code that names it compiles.

The C# the build reads today is read at two moments. A capability (`builder.Capabilities.UseCamera`) is
read by a source generator during the compile, because what it feeds (a manifest) is written after it. The
app icon (`AppIcon.cs`) is compiled by eqicon on its own, against the vocabulary, because it is drawn
before the app exists.

## Goals / Non-Goals

**Goals:** a provider's face declared in C#, fetched by the build, pinned by the app, embedded in the app
and served by it on every target, with no request from a page to the provider.

**Non-Goals:**
- Providers other than Google Fonts. The declaration is one attribute per provider, so a second one is a
  second attribute and a second client, and nothing downstream changes.
- Weights outside `FontWeight`'s members. The vocabulary names 400 to 800 today, and text cannot be set
  in another weight either, so the declaration takes what the vocabulary names (#1082).
- A variable file. Google serves a client that is not a browser one static file per weight and style
  (measured below), which the catalog already reads. Photon's handling of a variable file's axis is #1061.
- Fetching at run time, which is the default of Flutter's `google_fonts`: a request would leave every
  page for the provider, and the text would change when the provider's file did.

## Decisions

- **How Flutter does it.** `google_fonts` fetches a face at run time by default, caches it, and checks
  each file against a SHA-256 the package pins. A face that must not be fetched is bundled by hand: the
  developer downloads the files into the assets and declares them in `pubspec.yaml`. Here the build is the
  fetcher, which is the bundled path without the hand download, and the pin is the app's own rather than
  a package version's, so an app renews it when it chooses rather than when a package moves.
- **An attribute, read before the app compiles.** A face has to be embedded in the same compile that
  names it through the catalog, so its declaration has to be read before the compiler runs, which a
  generator cannot do. `[assembly: GoogleFont("Inter", FontWeight.Regular, FontWeight.Bold, Italic = true)]`
  is C#, typed, and constant by construction. MSBuild hands eqicon the files that mention it, and eqicon
  reads their syntax alone, with the compile's own symbols (after the .NET SDK adds its implicit ones, such
  as `NET10_0_OR_GREATER`) so a declaration under `#if` is read when the compiler sees it. It knows the
  attribute by its own name, so an alias of it, local or global, is EQ4007 at the alias rather than a
  declaration passed over in silence: the family is a string literal, each weight a `FontWeight` member, the italic
  `true` or `false`. Anything else fails the build with EQ4007 at the attribute, as EQ3003 does for a
  capability's reason built at run time: the build reads the declaration before any constant elsewhere in
  the app exists. The compiler still binds the attribute when the app compiles, so a misspelt member
  fails there as well.
- **Before the resources are read.** The fetched files are added as `EmbeddedResource` items, with
  `EQuanticFont` set as the `Assets` faces have it, from a target that runs before `AssignTargetPaths`.
  Measured: a target before `PrepareResources` runs after the resource pipeline has read the items, and
  the face was not embedded. Before `AssignTargetPaths` it is, under its logical name. eqicon's catalog
  then reads the family from the file, as it does for every face, so `Fonts.Inter` is written beside the
  `Assets` families and nothing downstream knows a provider exists. Each file is embedded as
  `eQuantic.UI.Fonts/google/<Family>-<weight>[-italic].ttf`, a folder no file under `Assets` is embedded
  in, so a provider's face never collides with one the app drew from a file.
- **Pinned by the app, as NuGet pins a package.** `fonts.lock.json`, beside the project, holds each file's
  URL and the SHA-256 of its bytes, by provider, family and cut. The SDK writes it the first time it
  fetches a file, and the app commits it as it commits `packages.lock.json`. A pinned file is fetched from
  its pinned URL, never asked of the provider again, and checked against its hash: bytes that differ, or a
  URL that no longer answers, fail the build with EQ4009, naming the file and the way to renew it, which
  is to take its family out of the lock. A family is pinned whole: a cut added to a family whose pinned
  cuts the provider has since replaced fails the same way, so one family never mixes two of the
  provider's versions. No build takes a pin out of the lock: a family stays pinned until the app takes it
  out, because the families a build reads depend on its symbols, one set per configuration and target
  framework, and a Release build that does not see a family declared under `#if DEBUG` is no reason to
  forget it (Copilot's review of #1085). A build that fails leaves the lock and its folder as they were.
- **Fetched once, out of the source tree, and laid out per build.** The lock and a store of fetched files,
  `obj/equantic/fonts`, each file named by its SHA-256 and checked against its pin on every build, are the
  project's, so a rebuild fetches nothing and an offline build works once the files are there. What a build
  embeds is its own: once every declaration it reads has succeeded, eqicon lays out its files in
  `$(IntermediateOutputPath)equantic/fonts/google`, one a cut, named by the family and the cut, copied from
  the store and nothing else, and the target embeds that folder by a glob, as `Assets` is (MSBuild cannot
  pair a list of paths and resource names into items). A build that fails lays out nothing, so what it
  fetched for a family that did succeed waits in the store, embedded nowhere. The builds of a Photon app's
  target frameworks may run at once: they take turns over the store and the lock through a file held open
  with no sharing, so none writes a lock without the pins another just added. A file is written whole,
  through a file beside it, renamed over it, and one laid out already with the same bytes is left alone,
  so the compile does not run again for it.
- **The provider's own API, as a client that is not a browser.** `fonts.googleapis.com/css2` is the API
  Google documents. Measured, it answers a client that is not a browser with one TrueType file per weight
  and style, a range of weights included, at a versioned URL (`/s/inter/v20/…`). A family, weight or
  italic it does not have is answered with 400 and one sentence for all three ("The requested font
  families are not available."), so on a 400 the build asks again, for the family alone and then for each
  cut, and names what is missing (EQ4008). A provider that cannot be reached, with a file not fetched yet,
  fails the same way, saying so.
- **Design-time builds fetch nothing.** An editor's background build embeds what the last build fetched
  and asks nobody, so typing never reaches the network.
- **The instrument fails.** Every outcome that would change the app's text without the developer choosing
  it fails the build: a changed file, a vanished one, a family the provider moved on, a declaration the
  build could not read. Nothing warns and continues.

## Risks / Trade-offs

- Google answers TrueType to a client that is not a browser by behaviour, not by contract. Should it
  answer a web font instead, eqicon's catalog refuses it (EQ4005) and the build says so: it fails, it does
  not ship a different face.
- A clone with no `obj` fetches every pinned file again, from its pinned URL. The pin keeps the bytes the
  same, and a provider that has dropped the URL fails the build until the family is renewed.
