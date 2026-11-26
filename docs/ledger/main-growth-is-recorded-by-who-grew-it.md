- **2026-11-26 · main's unrecorded served-runtime growth is recorded by who grew it**: `main` had grown 3,907 bytes
  gzipped past the base and its deltas, from #1852's record to f79bec9da, without a change recording them, and
  #1913, whose own growth is 106 bytes, met the ceiling. Measured at every first-parent merge as
  `docs/SERVED-RUNTIME-GROWTH.md` builds its tables, the 3,907 are an entry of their own,
  `main-growth-to-f79bec9da.json`, each merge's bytes in its row, rather than inside the delta of the change that met
  the ceiling ([#1922](https://github.com/eQuantic/equantic-ui/issues/1922)).
