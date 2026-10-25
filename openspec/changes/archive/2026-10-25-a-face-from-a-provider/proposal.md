## Why

Closes #1057, a user story under #1038 (the document and its faces come from the theme), under the epic
#1035. Since #1055 an app ships a face by dropping its files under `Assets`, and since #1056 a page may
name any family that ships files. A face from a provider such as Google Fonts has no way in: the
developer downloads its files by hand and commits them, or loads them from the provider at run time,
which sends every page's reader to a third party and changes the app's text whenever the provider changes
a file.

## What Changes

For a developer using the SDK, one line of C#, anywhere in the app:

```csharp
[assembly: GoogleFont("Inter", FontWeight.Regular, FontWeight.Bold, Italic = true)]
```

and the family is there to name, as a face under `Assets` is: `Fonts.Inter` in the app's font catalog.

- **The build fetches the files** before the app compiles, from Google Fonts' own API, and keeps them
  out of the source tree, under `obj`, so a rebuild fetches nothing and an offline build works once they
  are there.
- **Each file is pinned** in `fonts.lock.json` beside the project, written by the SDK the first time it
  fetches the file and committed like NuGet's `packages.lock.json`: its URL and the SHA-256 of its bytes.
  A pinned file whose bytes differ, or whose URL no longer answers, fails the build, and so does a family
  the provider has moved on from since its cuts were pinned, so a provider never changes the app's text
  in silence. The failure says how to renew: take the family out of the lock, and the next build pins the
  provider's current files.
- **The app serves them itself.** The files are embedded in the app's assembly and carried by its catalog,
  so the server serves them as it serves a face under `Assets`, and a Photon app registers them from its
  own assembly. No page asks the provider for anything.
- **A declaration the build cannot honour fails it**, naming the place: a family, a weight or an italic
  the provider does not have, a provider that cannot be reached with nothing fetched yet, and arguments
  that are not literals, since the build reads them before the app compiles.

The parts it reaches: the vocabulary (`GoogleFontAttribute`), eqicon (a `fetch-fonts` verb), and both
SDKs (a target that embeds the fetched files before the resources are read). The public surface adds
`GoogleFontAttribute` to `eQuantic.UI.Primitives`. The developer surface adds no property: the lock file
is the SDK's, as `packages.lock.json` is NuGet's. Nothing an app wrote breaks.
