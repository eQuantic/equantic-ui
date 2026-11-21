- **2026-11-21 · A page hydrates in its tests**: a real site's pages passed every test of the server's HTML
  and broke only once they hydrated, and the last of them said nothing even in development.
  `HydrationAssert.PageAsync(factory, "/catalogue")`, in `eQuantic.UI.Testing`, serves the app on a port,
  runs the page's scripts against the server's markup in happy-dom under the embedded bun the app was built
  with, and fails naming an error the page reported, a text, an attribute or an element the browser drew
  otherwise, or a page the router chose that the server did not draw; hydration now records what it wrote
  over, and the browser says in development when its router and the server chose different pages, at load
  and after a navigation. A site of pages alone, with no component of its own, failed its build on the
  generated-sources prune, and builds
  ([#1775](https://github.com/eQuantic/equantic-ui/issues/1775),
  [#1778](https://github.com/eQuantic/equantic-ui/issues/1778)). Proposed and archived through OpenSpec
  (`openspec/specs/component-testing`, `hydration-contract`, `page-routes`).
