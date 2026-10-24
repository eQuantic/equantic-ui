- **2026-10-24 · The document is the theme's**: the first slice of #1038, under the epic of any design
  system in C# with no CSS (#1035). `IAppTheme` names the document around the components: `TextFamily`
  beside `MonoFamily`, and `Selection`, `OnSelection`, `Placeholder`, `ScrollbarThumb` and
  `ScrollbarTrack`, each defaulting to the value the SDK already drew with, so Photon's goldens hold. The
  web's theme stylesheet writes the body, the selection, every field's placeholder and the page's
  scrollbars from them, on every page (Photon's theme when the app chose none, which also defined the
  variables a table's scrollbars named and nothing declared), and it comes before the app's own CSS.
  `HtmlShellOptions.SetBaseStyles` and `BaseStyles` gave way to `AddStyles`, which appends. Photon sets
  a field's text through the merge a `Text` goes through, and a field's placeholder, a table's
  scrollbars and the code editor's selection read the slots on both targets. On the way,
  `IAppTheme.cs`, `UIExtensions.cs` and `TokenCss.cs` split into one type per file, the stylesheet
  generator was renamed `ThemeStylesheet`, and twelve copies of a test rasterizer became one
  ([#1038](https://github.com/eQuantic/equantic-ui/issues/1038)). Proposed and archived through
  OpenSpec (`openspec/specs/document-theme`, a new spec, and `typography`).
