- **2026-11-26 · Each upload type has a file of its own, and main passes the one-type-per-file guard again**:
  #1891 added `UploadPlace` at the end of `UploadStore.cs` and `UploadsStorageChoice` at the end of
  `UploadsBuilder.cs`, two second top-level types the one-type-per-file baseline does not list, so
  `OneTypePerFileTests` failed on main and every merge group built on it. Each type moved, unchanged, to a
  file named for it; the baseline is untouched, since a new file never earns an entry
  ([#1893](https://github.com/eQuantic/equantic-ui/issues/1893)). No behaviour changes.
