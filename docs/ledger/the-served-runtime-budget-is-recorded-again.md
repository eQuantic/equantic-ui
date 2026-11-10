- **2026-11-10 · The served runtime's budget is recorded again, at 324,551 bytes gzip**: `main` went
  over the ceiling, floor(320,826 × 1.01) = 324,034 bytes, at 11ee6d152 (#1338, 324,277), and #1345
  took it to 324,551. Seventeen pull requests touched the runtime since #1219 recorded the budget.
  Measured with the build `BundleRuntime` runs at each one's merge and gzip at
  `CompressionLevel.SmallestSize`, the runtime went from 320,825 to 324,551 bytes, +3,726 gzip and
  +14,346 raw: #1255 +929, #1235 +596, #1341 +498, #1283 +370, #1248 +321, #1338 +283, #1345 +274,
  #1229 +179, #1233 +167, #1325 +54, #1282 +38, #1257 +17, and none from #1251, #1316, #1184, #1348
  and #1350. Nothing stopped it: #1341 merged with `test (windows-latest, rest)` red, #1338 and #1345
  before their test jobs ended, and the ruleset still required the two test legs #1323 had replaced.
  None of the growths is out of proportion with what its pull request does, but the measuring showed
  the dev loop, about 30 KB of the minified runtime, carried to every production page (#1359)
  ([#1357](https://github.com/eQuantic/equantic-ui/issues/1357)).
