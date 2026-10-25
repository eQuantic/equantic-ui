- **2026-10-25 · A face from Google Fonts is fetched by the build and served by the app**: the fourth slice of
  #1038 ([#1057](https://github.com/eQuantic/equantic-ui/issues/1057)). An app declares a family in C#,
  `[assembly: GoogleFont("Inter", FontWeight.Regular, FontWeight.Bold, Italic = true)]`, and the build fetches
  the file of each cut before the app compiles (eqicon's `fetch-fonts`, from a target before
  `AssignTargetPaths` in both SDKs), keeps it under `obj/equantic/fonts/google` and embeds it as the faces
  under `Assets` are, so the catalog names `Fonts.Inter` and the server and Photon load it from the app. No
  page asks Google for anything. Each file is pinned in `fonts.lock.json` beside the project, by its URL and
  its SHA-256, written by the SDK and committed by the app as NuGet's `packages.lock.json` is: a pinned file
  that changed or vanished, or a cut added to a family Google has replaced, fails the build (EQ4009) until the
  family is taken out of the lock. No build takes a pin out, since the families a build reads depend on its
  symbols, each build embeds from a folder of its own, and a build that fails leaves the lock as it was. EQ4007 refuses a
  declaration the build cannot read before the compile, and EQ4008 a family, weight or italic Google does not
  have, or Google out of reach. The weights outside `FontWeight` are #1082. Proposed and archived through
  OpenSpec (`font-faces`).
