- **2026-10-29 · The runtime's test tooling moves past eleven advisories**: a task under #306
  ([#1177](https://github.com/eQuantic/equantic-ui/issues/1177)). Dependabot reported eleven open advisories
  against the runtime's lockfile, all in the tooling that proves it: `happy-dom` (to 21.0.4, a major whose one break
  disables CSS and iframe loading by default), `vite` (6.4.5), `vitest` and `@vitest/mocker` (4.1.12),
  `source-map-js` (1.2.2) and `brace-expansion` (1.1.21 and 5.0.12). None reaches what ships: the served
  `runtime.js` imports no npm package, and it came out byte-identical. `bun.lock`, which `TestRuntime` installs
  from, moved by `--lockfile-only` and in place, because a fresh resolution moved some sixty packages and gave
  vitest a vite 8 of its own. `package-lock.json`, which Dependabot reads, records the same 233 versions; that
  two lockfiles exist at all is #1180. happy-dom 21 brought `Element.animate`, and with it a `cancel()` that
  leaves its rejected finished promise unhandled where Web Animations marks it handled: the table glides failed
  the run with rejections no browser raises, and the test environment restores the standard (`setupFiles`).
