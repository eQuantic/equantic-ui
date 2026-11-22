- **2026-11-22 · A page that imports a chunk for its side effects hydrates in its tests**: `HydrationAssert`
  failed on 15 of a real site's 18 pages with `Cannot find module`, because its harness rewrote a side-effect
  import into the path its plugin fetches only when a space followed `import`, and a minified bundle writes
  `import"./chunk.js"`. The space is optional now. `tests/HydrationSite` gains the graph that makes the bundler
  write one (a component two pages reach only through another), and its test asserts the import is in the
  page's module ([#1804](https://github.com/eQuantic/equantic-ui/issues/1804)). Proposed and archived through
  OpenSpec (`openspec/specs/component-testing`).
