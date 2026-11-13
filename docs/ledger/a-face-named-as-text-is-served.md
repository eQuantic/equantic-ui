- **2026-11-10 · A face outside the theme is served at its identity, and a single-file app serves one it
  names only as text**: a page wrote each face outside the theme's stylesheet at its content-addressed URL,
  so every instance had to know which faces the deployment carries, and read it from the host's list of
  assemblies and a walk through references. A single-file app's host lists none of the assemblies its
  bundle carries, and a family that names its library only as text leaves no reference: measured with a
  web app published as a single file, the page declared nothing and the identity answered 404. Such a face
  is now named by its identity with the hash of its bytes (`/_equantic/fonts/<assembly>/<resource>?v=<hash>`),
  which any instance answers by loading the library by its name, as Photon reads it, and the scan is gone
  ([#1080](https://github.com/eQuantic/equantic-ui/issues/1080)). Proposed and archived through OpenSpec
  (`openspec/specs/font-faces`).
