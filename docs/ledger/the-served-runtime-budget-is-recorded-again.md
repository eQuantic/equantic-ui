- **2026-11-10 · The served runtime's budget is recorded again, at 324,551 bytes gzip**: `main` went
  over the ceiling at 9014ee883, 324,551 bytes against floor(320,826 × 1.01) = 324,034, though no pull
  request failed: seventeen touched the runtime since #1219 recorded the budget, each under the 1% on
  its own. Measured with the build `BundleRuntime` runs at each one's merge and gzip at
  `CompressionLevel.SmallestSize`, 320,825 then 324,551 bytes, +3,726 gzip and +14,346 raw: #1255
  +929, #1235 +596, #1341 +498, #1283 +370, #1248 +321, #1338 +283, #1345 +274, #1229 +179, #1233
  +167, #1325 +54, #1282 +38, #1257 +17, and none from #1251, #1316, #1184, #1348 and #1350. None is
  out of proportion with what it does, but the measuring showed the dev loop, about 30 KB of the
  minified runtime, carried to every production page (#1359)
  ([#1357](https://github.com/eQuantic/equantic-ui/issues/1357)).
