- **2026-11-09 · An optimized image is cached once by its file**: a Bug from the security review (#1265),
  [#1289](https://github.com/eQuantic/equantic-ui/issues/1289). The image optimizer's cache was keyed by the
  url's spelling and any quality from 1 to 100, and bounded by age alone. It is keyed by the file the url
  resolves to, a request's quality is rounded to one of `Qualities`, and the cache keeps at most
  `MaxCacheSize` on disk, the oldest copies going first. Measured on main, the four new middleware tests
  fail there. Proposed and archived through OpenSpec (`image-optimization`).
