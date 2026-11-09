## Why

Closes #1289, a Bug the security review (#1265) found in the image optimizer.

The cache was keyed by the url as the request spelled it and by any `q` from 1 to 100, and bounded by age
alone: `/img/a.png`, `//img/a.png` and `/img/./a.png` were three decodes, three encodes and three copies of
one file, kept for the cache's lifetime, so an anonymous client could spend the server's CPU and disk.

Measured on main, the four middleware tests this change adds all fail there.

## What Changes

- **The key is the file**: the path it resolves to under the web root, with its size and its time, so a
  file replaced on disk is a new entry. Case is folded where the file system folds it.
- **The quality is one of a set**: `Qualities` (`50, 75, 90, 100`), a request's `q` rounded to the nearest;
  `DefaultQuality` must be one of them, or the app stops at start.
- **The cache has a cap**: `MaxCacheSize` (512 MiB). Past it the oldest copies are removed until it holds
  nine tenths of its cap.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `image-optimization`: one new requirement, a bounded cache keyed by the file.

## Impact

- **eQuantic.UI.Images**: `ImageOptimizationOptions` (`Qualities`, `MaxCacheSize`, `QualityFor`),
  `ImageOptimizationMiddleware`, `ImageCache`.
- **For an app**: a `DefaultQuality` outside `Qualities` stops the app at start; the Upgrading page says what
  to write.
