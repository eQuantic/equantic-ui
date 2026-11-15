- **2026-11-12 · The browser builds through the element engine**: the first slice of E3b. The browser's
  components build through the engine's owners, one per root a render lowers, in place of the instance
  store: the lowering lowers what each position built and keeps the instance each position keeps, a
  nested component's `SetState` builds its own element and no other before the page lowers again, and
  hydration builds the element tree before it walks the server's DOM. In the same pull request every arm
  of an adaptive node became a position on the web, which draws them all behind its media gates, and a
  composed component's server data is keyed by where it stands (`r{tree}/{type}@{slots}`) instead of
  `Type#ordinal`, whose count the server and the browser made in two different orders once the browser
  built through the engine; the engine records each position's slot for it
  ([#1453](https://github.com/eQuantic/equantic-ui/issues/1453),
  [#1477](https://github.com/eQuantic/equantic-ui/issues/1477)). The e2e suite in a real browser passed
  15 of 15 before and after. Proposed and archived through OpenSpec, adding three requirements to
  `element-tree` and modifying the server's.
