# What each pull request added to the served runtime

The runtime the Server embeds and every page loads first, measured before and after each merge on
`main` since its budget was recorded with #1219, the way `ServedRuntimeBudgetTests` measures it. Written
for #1357, when `main` went over that budget and no merge had been stopped by it.

## How it was measured

At every first-parent commit of `main` from #1219 (2966e8208) to 0.2.0-preview.68 (0efe00df1), 45 in
all, the bundle was built as the Server's `BundleRuntime` target builds it, from
`src/eQuantic.UI.Server`, with the embedded bun of this repository (1.4.3, which no commit of the range
changed):

```bash
bun build ../eQuantic.UI.Sdk/Resources/boot.ts --outdir <dir> --entry-naming 'runtime.[ext]' \
  --sourcemap=external --minify-syntax --minify-whitespace
```

Each `runtime.js` was then compressed with .NET's `GZipStream` at `CompressionLevel.SmallestSize`, the
test's own measure. The method reproduces the test: at 0efe00df1 it gives 324,551 bytes, which is what
the test measured on `main` and what #1365 recorded. At #1219 it gives 320,825, one byte under the
320,826 that #1219 recorded. The runtime has no package dependency, so nothing outside the tree entered a
bundle. The bundles were built on macOS (arm64).

A merge that changed no byte of the bundle is listed only when it touched a source of it (a file under
`src/eQuantic.UI.Runtime/src` other than a test or a fixture, or `boot.ts`). The other 27 merges of the
range changed nothing in it.

## The table

Sizes are gzip bytes, as the budget counts them. Raw is the minified bundle's growth.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1229 | 060d4fb25 | 320,825 | 321,004 | +179 | +333 | an embedded document is handed what its page may use | yes |
| #1235 | 4b2129118 | 321,004 | 321,600 | +596 | +1,555 | a string search says its comparison, and text is written as .NET writes it | yes |
| #1255 | 2d97110f1 | 321,600 | 322,529 | +929 | +4,608 | `GetType()` comparisons, and the twelve argument guards with .NET's own messages | yes |
| #1251 | 7877e6388 | 322,529 | 322,529 | 0 | 0 | an app's icon declared once | yes |
| #1316 | 2a4319589 | 322,529 | 322,529 | 0 | 0 | a Server Action authorized by a page's rule | yes |
| #1184 | 65c386dd0 | 322,529 | 322,529 | 0 | 0 | the runtime's test tooling past eleven advisories | yes |
| #1248 | afb053599 | 322,529 | 322,850 | +321 | +1,034 | an enum held at its underlying type's width, a 64-bit one as a BigInt | yes |
| #1257 | c17410bde | 322,850 | 322,867 | +17 | +63 | a Photon field keeps what was typed | yes |
| #1282 | 8d157899a | 322,867 | 322,905 | +38 | +455 | a pressed fill as its surface's Pressed diff, across 22 transpiled components | yes |
| #1233 | 39684a3d4 | 322,905 | 323,072 | +167 | +697 | a container sees through a component to what only it can place | yes |
| #1348 | 4de634269 | 323,072 | 323,072 | 0 | 0 | a twin says the type C# declared (types only) | yes |
| #1350 | c5c8c3669 | 323,072 | 323,072 | 0 | 0 | a class's twin passes the type check (types only) | yes |
| #1283 | 010543f29 | 323,072 | 323,442 | +370 | +1,601 | a hot reload hydrates the page when the server took the edit | **no**: dev-only code in every production page, #1359 |
| #1325 | d88e19a44 | 323,442 | 323,496 | +54 | +191 | the Content-Security-Policy as a builder setting | yes |
| #1341 | 70d8dfb27 | 323,496 | 323,994 | +498 | +1,601 | a page offers a file it made for the person to save | yes |
| #1338 | 11ee6d152 | 323,994 | 324,277 | +283 | +864 | a link's scheme allowed by a list, and a style element's text kept inside it | yes |
| #1345 | 9014ee883 | 324,277 | 324,551 | +274 | +1,344 | the same screens draw alike on the web and on Photon | yes |
| **all** | | **320,825** | **324,551** | **+3,726** | **+14,346** | | |

#1338 took `main` over the ceiling the test allows, floor(320,826 × 1.01) = 324,034, and #1345 took it
further. Neither growth is out of line with what its pull request did: the runtime crossed its ceiling
by the sum of eleven growths, every one but #1283's in line with what it delivered, and no test job
gated a merge at the time (#1357 says why).

## The one growth out of proportion

#1283's 370 bytes are code a production page never runs. `boot.ts` imports the dev loop statically
(`dev/error-overlay`, `dev/hot-reload-state` and what they pull in) and decides at run time whether to
use it, so every growth of the dev loop is paid by every page an app serves. bun's metafile of the same
build puts the dev modules at 29,920 of the bundle's 1,296,538 bytes, about 7 KB of gzip, before the
boot's own hot-reload half. That is #1359, a Bug under #1174, whose fix serves the dev loop as a module
of its own that the boot imports only when the server says it is on.

## What a change costs from here

The runtime is one bundle, so a feature added to it is paid by every page of every app, whether the app
uses it or not. #1341's file saver is the plainest case: in proportion with what it delivered, and loaded
by pages that never offer a file. That is the design, not a defect of any one pull request, and the
budget is what makes each such cost a decision: past 1% of the record a change fails the test and has
to record the budget again, saying what the bytes bought.

## Since the record of #1405

#1405 recorded 328,530 bytes, measured on a `main` that did not have #1482 yet; its own merge
(28a4f7fb0), over a `main` that did, measures 329,095. #1482 grew the bundle by 577 bytes (322,053 to
322,630), which accounts for the gap. From that merge, the bundle was measured as above at every
first-parent commit of `main` that touched a source of it, and #1458 over `main` at 1973005e6, the
state that crossed the ceiling, floor(328,530 × 1.01) = 331,815. #1499 touched no source of it.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1497 | f297815d4 | 329,095 | 329,524 | +429 | +963 | a gesture on the web reads the space its surface's offset moves in | yes |
| #1408 | 1973005e6 | 329,524 | 331,014 | +1,490 | +5,842 | a theme slot reaches a component in order, crosses to the browser, and the Button and the control ladder read theirs | yes |
| #1458 | | 331,014 | 331,839 | +825 | +2,595 | a table takes what is left of its scroller, measured on both targets | yes |
| **all** | | **329,095** | **331,839** | **+2,744** | **+9,400** | | |

#1458 took the bundle 24 bytes over that ceiling and did not record it: #1472, about to merge, was
recording the budget again over all of the above.

## Since the record of #1472

#1472 recorded 332,993 bytes before it merged; its merge (4b82ea5c9) measures 333,519. From that merge,
the bundle was measured as above at every first-parent commit of `main` that touched a source of it, and
#1458 over `main` at be8b33109. #1433 recorded the budget again at 337,565 bytes, also before it merged;
its merge (92b0af2a8) measures 339,508, under that record's ceiling, floor(337,565 × 1.01) = 340,940.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1502 | 651a0567f | 333,519 | 334,283 | +764 | +3,646 | a value held as object keeps the type its answers depend on (the runtime's type marks) | yes |
| #1428 | 4cd212fba | 334,283 | 336,243 | +1,960 | +8,337 | the surfaces, menus, notices, progress and cards read their theme slots | yes |
| #1384 | 64cd02bf2 | 336,243 | 336,251 | +8 | +42 | contrast levels resolve beside the theme mode, from the system's own setting | yes |
| #1433 | 92b0af2a8 | 336,251 | 339,508 | +3,257 | +12,772 | navigation and collections read their theme slots, and the document's style becomes one | yes |
| #1520 | fbde35b8a | 339,508 | 339,521 | +13 | +44 | a new key is made without a pass over the old list | yes |
| #1521 | be8b33109 | 339,521 | 340,025 | +504 | +2,484 | a Select groups its options, on the web and on Photon | yes |
| #1458 | | 340,025 | 340,855 | +830 | +2,595 | a table takes what is left of its scroller, measured on both targets | yes |
| **all** | | **333,519** | **340,855** | **+7,336** | **+29,920** | | |

#1458 stays inside #1433's ceiling, 85 bytes under it, and records nothing. Its own 830 bytes are the
table's fit, the same 2,595 raw bytes it added over 1973005e6 above, which gzip packs a little
differently beside different neighbours. The theme slots (#1428 and #1433) are the largest share since
#1472: a feature every page with a themed surface uses. 85 bytes is less than any feature adds, so the
next change that grows the runtime records the budget again.
