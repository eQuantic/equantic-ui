- **2026-11-10 · A served eqc compiles again only the files a save can have changed**: with the
  compilation kept and a module written only when its text changed, a save still transpiled every file
  of the app, 132 on the dashboard sample. After a save that changed bodies alone, the build now
  compiles the files it changed, the files whose last compile read one of them, and the files whose
  last compile failed, and takes every other file as the build before compiled it, its strings handed
  to the catalogs again. A save that changes a declaration compiles every file. What a compile read is
  recorded as it reads it: every path from a symbol to syntax in the compiler goes through one
  accessor that notes the file, the two caches that keep an answer read from another file hand the
  read on, and a guard fails on a read that does not pass there. Measured on the dashboard sample with
  the same instrument: a save compiles 1 file of 132, eqc's part went from a median of 5.6s (load 56 to
  64) to 0.34s, and the total from the save to the frame from 5.8s to 0.6s, every warm run under a
  second, at a load average of 126 to 140
  ([#1281](https://github.com/eQuantic/equantic-ui/issues/1281)). Proposed through OpenSpec
  (`openspec/specs/hot-reload`).
