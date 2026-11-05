- **2026-11-05 · A wiki snippet implements an SDK interface as the interface is**: the DesignSystem
  page's "The document is the theme's" went on writing `public string? TextFamily => "…"` in all five
  languages after `TextFamily` became a `FontFamily?` in 0.2.0-preview.66, because the snippet was
  never transcribed into `WikiClaimsCompile`, the only thing that compiled the wiki. The page writes a
  `FontFamily` now, and `WikiImplementationsTests` compiles every C# block of every page and language
  against the SDK's assemblies and compares each member a type writes over an SDK interface with the
  interface's own, 160 members in 40 pages, with no transcription. It failed on the wiki's master
  naming the five copies and their lines. `eQuantic.UI.Web.Tests` references `eQuantic.UI.Server`,
  without which 35 snippets over the server's contracts went unread, and an SDK interface the guard
  cannot resolve fails it ([#1226](https://github.com/eQuantic/equantic-ui/issues/1226)). Proposed and
  archived through OpenSpec (`openspec/specs/working-agreement`).
