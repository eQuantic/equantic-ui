# Proposal

Closes #1080, a Bug under #1038.

## Why

A family a page names outside the theme's stylesheet is served on the web when every file of it is a
face an eQuantic build embedded, and every instance of the app has to answer its URL, since a page one
instance rendered asks another for its font. The page wrote each such file's content-addressed URL, so an
instance had to know which faces the deployment carries to answer it, and it read that set from two
places: every assembly the host lists for the deployment (`TRUSTED_PLATFORM_ASSEMBLIES`) and a walk from
the app's assemblies through the references in their metadata.

A single-file app's host lists none of the assemblies its bundle carries, and a family that names its
assembly only as text, `new FontFile("Acme.Fonts", "eQuantic.UI.Fonts/Brand.ttf", 400, 400)` with no type
of the library used, leaves no reference for the walk to follow. Measured with a web app published as a
single file over such a library: the page declared nothing, the identity answered 404, the server logged
that no eQuantic build had embedded the face, and the text fell back on the web while it loads on Photon,
which reads the face by loading the assembly by its name.

## What Changes

- A face outside the theme's stylesheet is named on a page by its identity, with the hash of its bytes as
  its version: `/_equantic/fonts/<assembly>/<resource>?v=<hash>`, the shape `asp-append-version` writes.
  Any instance answers it by loading the assembly by its name, as `FontFile.Read()` does on Photon, so no
  instance needs to know which faces the deployment carries, and the scan of the host's list and the walk
  through references are gone.
- The identity route sends the file's bytes, immutable, when the request names the version the file has,
  and otherwise redirects to the URL a page names the file by, as before: a page the browser builds asks
  without a version, and a page a new build replaced asks with an old one.
- The route loads only an assembly named by its exact simple name, never one of the platform's own, and
  reads only a face an eQuantic build embedded. A name the loader resolves to an assembly spelled
  otherwise answers 404, so one file is one URL, read and compressed once.
- The theme's stylesheet keeps its content-addressed URLs and preloads: every instance reads it from the
  theme when it starts, and the content-addressed route now serves only those files.
- An app writes nothing new. No public surface and no developer surface moves.

## Impact

The server's font serving (`ServedFonts`, the two font routes in `UIExtensions`) and a comment in the
browser runtime's face declarations, which already asked for a file by its identity and follows the
redirect. Photon is untouched.
