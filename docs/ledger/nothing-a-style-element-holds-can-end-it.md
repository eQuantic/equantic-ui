- **2026-11-09 · Nothing a style element holds can end the element**: the server wrote the atomic sheet,
  the theme's sheet and an app's own styles into `<style>` as they came, so a value could end the element
  it was written in. `StyleElementText` is now the one
  writer of that text and writes every `</` as `\3c /`, which CSS reads as the same characters inside a
  string or a `url()` ([#1295](https://github.com/eQuantic/equantic-ui/issues/1295)). Proposed and
  archived through OpenSpec (`openspec/specs/document-theme`).
