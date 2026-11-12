- **2026-11-10 · A production page loads no byte of the dev loop**: a Bug under #1174
  ([#1359](https://github.com/eQuantic/equantic-ui/issues/1359)). The boot imported the error overlay and the
  hot-reload state statically, about 30 KB of `src/dev/` in the runtime every page loads first, and only decided at
  run time to use them. The runtime is now two bundles: `runtime.js`, with no module of `src/dev/`, and
  `runtime.dev.js`, the same boot with the dev loop installed through one seam, and the server names the one a page
  imports in its import map, the dev one in Development and under hot reload. A production page downloads 30,491
  bytes less, 8,828 of gzip, and the served runtime's budget records it.
