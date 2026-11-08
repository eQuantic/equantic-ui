# Proposal

Closes #1080, a Bug under #1035 (any design system in C#): a family a single-file app names only as text was
declared on no page, and its text fell back on the web while it loaded on Photon.

## Why

A page declares a face outside the theme's stylesheet when an eQuantic build embedded it, and every instance of
the app has to answer the URL the page names, since a page one instance rendered asks another for its font. #1077
gave every instance the same set by reading the deployment: the assemblies the host lists in
`TRUSTED_PLATFORM_ASSEMBLIES`, and a walk from the app's assemblies through the references in their metadata. A
single-file app lists none of the assemblies its bundle carries there, so the walk was all there was, and a
family that names its assembly only as text, `new FontFile("Lib", "eQuantic.UI.Fonts/Brand.ttf", 400, 400)` with
no type of `Lib` used anywhere, left no reference for it to follow. Measured with an app published single-file
over such a library: the page declared nothing and the server logged that the face was not served.

## What Changes

A face outside the stylesheet is served at its IDENTITY, its assembly and its resource, with the hash of its
bytes beside them, `/_equantic/fonts/<assembly>/<resource>?v=<hash>`, the shape `asp-append-version` writes. Any
instance answers it by reading the file as `FontFile.Read()` does, so no instance needs to know the deployment's
faces at all, and the set read from the deployment is gone: the host's list, the walk and the metadata reader.

- A page the server renders declares such a face at that URL; a file the theme's stylesheet serves keeps the
  stylesheet's content-addressed URL, so every page shares one cached copy of it.
- The identity route sends a URL with the file's current version as the bytes, immutable, and sends one without
  a version, or with a version a new build replaced, on to the URL the server writes for the file.
- Only a face an eQuantic build embedded, under the prefix both SDKs write, is read this way, from an assembly
  named by a simple name that is not the platform's.
- The content-addressed route serves the stylesheet's files, which every instance reads when it starts.

For a developer: nothing to write. A family a library ships is served whether or not the app's code uses a type
of that library, and whether or not the app is published as a single file.

## Capabilities

### Modified Capabilities

- `font-faces`: a face outside the stylesheet is served at its identity and version, by every instance.

## Impact

- `src/eQuantic.UI.Server/ServedFonts.cs`, `ServedFont.cs` and the two font routes in `UIExtensions.cs`.
