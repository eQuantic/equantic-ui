## Why

Closes #1056, a user story under #1038 (the document and its faces come from the theme), under the epic
#1035. Since #1055 the web declares the faces of the families the theme names and of the app's own
catalog, and nothing else. A family a page names that is in neither, built by hand or read from a
library's catalog, loads on Photon, which registers a family the first time text asks for it, and falls
back to the platform's face on the web. The same C# draws in one face on a phone and in another in the
browser.

## What Changes

For a developer using the SDK, nothing to write: a `Text` whose style names a family with files is set
in that family on the web, wherever the family comes from, as it is on Photon.

- **A page the server renders** declares, in its head, the faces of every family its text names that
  the theme's stylesheet does not, and the server serves their files at the content-addressed URLs the
  theme's faces use, on every instance of the app alike. The web serves a face an eQuantic build embedded
  in the app's assemblies, or one the theme names: a family over a font a library embedded another way is
  declared on no page and logged, as is one missing a file.
- **A page the browser builds** declares a family the first time the runtime sets or measures text in
  it, unless the document already has it, from the server's head or an earlier page. Each file is asked
  for at its own address, `/_equantic/fonts/<assembly>/<resource>`, which the server answers with a
  redirect to the file's content-addressed URL, for a face an eQuantic build embedded and nothing else.
  A family one of whose files fails to load is taken back whole, whoever declared it, so its text falls
  back as a whole, as on Photon, the console names the file, and a page that measured text in it measures
  again.
- **Once.** A family named twice is declared once: on a page the server writes, and in the document the
  browser keeps.

The parts it reaches: the web realizer (`FaceSink`, the text's lowering), the server (`ServedFonts` as
a service, the render, the font routes) and the runtime (the face declarations, the text's lowering, the
measurer). The public surface adds `FaceSink` to the web realizer and changes
`FontFaceRules.ContentType` to take the resource's name, a member no release has shipped. The developer
surface does not move, and nothing an app wrote breaks.
