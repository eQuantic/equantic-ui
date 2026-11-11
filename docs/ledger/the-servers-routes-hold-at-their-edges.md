- **2026-11-09 · The module route and the culture redirect answer right at their edges**: a module found in
  the local directory is served with 200, a missing one is answered in plain text that names nothing
  outside Development, and the redirect to a prefixed language writes its path encoded, where a path with
  an accent answered 500 ([#1296](https://github.com/eQuantic/equantic-ui/issues/1296)). Proposed and
  archived through OpenSpec (`openspec/specs/page-routes`).
