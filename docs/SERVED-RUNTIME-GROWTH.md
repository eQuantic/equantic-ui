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
to record the budget again, saying what the bytes bought. Since #1489 it records a delta of its own,
`Budgets/served-runtime/<branch>.json` with `deltaBytes` and `bought`, beside the base the last release
folded, and the test holds the runtime to the base and every delta: two changes that each crossed the
ceiling used to rewrite the one number and conflict on it, as the sections below show. A release
folds the deltas into the base (`./scripts/fold-runtime-budget.sh`).

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
the bundle was measured as above at every first-parent commit of `main` that touched a source of it, up to
#1501's record. #1433 recorded the budget again at 337,565 bytes, also before it merged; its merge
(92b0af2a8) measures 339,508, under that record's ceiling, floor(337,565 × 1.01) = 340,940.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1502 | 651a0567f | 333,519 | 334,283 | +764 | +3,646 | a value held as object keeps the type its answers depend on (the runtime's type marks) | yes |
| #1428 | 4cd212fba | 334,283 | 336,243 | +1,960 | +8,337 | the surfaces, menus, notices, progress and cards read their theme slots | yes |
| #1384 | 64cd02bf2 | 336,243 | 336,251 | +8 | +42 | contrast levels resolve beside the theme mode, from the system's own setting | yes |
| #1433 | 92b0af2a8 | 336,251 | 339,508 | +3,257 | +12,772 | navigation and collections read their theme slots, and the document's style becomes one | yes |
| #1520 | fbde35b8a | 339,508 | 339,521 | +13 | +44 | a new key is made without a pass over the old list | yes |
| #1521 | be8b33109 | 339,521 | 340,025 | +504 | +2,484 | a Select groups its options, on the web and on Photon | yes |
| #1501 | 621f63310 | 340,025 | 341,447 | +1,422 | +4,392 | the browser builds through the element engine, and server data is keyed by a component's position | yes |
| **all** | | **333,519** | **341,447** | **+7,928** | **+31,717** | | |

The theme slots (#1428 and #1433) are the largest share since #1472: a feature every page with a themed
surface uses. #1501 took the bundle 507 bytes over #1433's ceiling and recorded the budget again.

## Since the record of #1501

#1501 recorded 341,447 bytes, and its merge (621f63310) measures exactly that. From that merge, the
bundle was measured as above at every first-parent commit of `main` that touched a source of it, up to
#1500's record.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1495 | 1b41cdc80 | 341,447 | 342,575 | +1,128 | +3,922 | the browser keeps a node's state on its position | yes |
| #1515 | 72ec4a5d9 | 342,575 | 343,496 | +921 | +2,984 | an app's layout stays mounted across client navigations | yes |
| #1500 | ac5a0975c | 343,496 | 346,041 | +2,545 | +9,518 | a dirty element in the browser lowers its own subtree | yes |
| **all** | | **341,447** | **346,041** | **+4,594** | **+16,424** | | |

#1500 took the bundle over #1501's ceiling, floor(341,447 × 1.01) = 344,861, and recorded the budget
again.

## Since the record of #1500

#1500 recorded 345,013 bytes before it merged; its merge (ac5a0975c) measures 346,041, under that
record's ceiling, floor(345,013 × 1.01) = 348,463. #1458 was measured over `main` at ac5a0975c.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1458 | | 346,041 | 346,847 | +806 | +2,595 | a table takes what is left of its scroller, measured on both targets | yes |

#1458 stays inside #1500's ceiling, 1,616 bytes under it, and records nothing. Its 806 bytes are the
table's fit, the same 2,595 raw bytes it added over 1973005e6 above, which gzip packs a little
differently beside different neighbours.

## Since the base of #1489

From #1489 the budget is the base the last release folded, 357,702 bytes, and a delta per change. `main`
at 62e5680d2 measures 357,984, inside that base's 1%.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1584 | | 357,984 | 361,698 | +3,714 | +29,011 | a LINQ query runs when it is walked, and a list changed while it is walked throws, as .NET's do | yes |
| #1620 | | 370,922 | 372,479 | +1,557 | +5,069 | a date is written and read in the calendar of its culture, th-TH's Thai Buddhist years, fa-IR's Persian calendar and ar-SA's Um Al Qura, as .NET counts them | yes |

It records a delta of 3,996 bytes, measured against the base rather than against `main`, which had grown
282 bytes past it. It is the largest single growth the runtime has had, so it answers the three
questions a reader asks of it.

**What the bytes buy.** An app observes two things it did not before. A query's callbacks run when the
query is walked, one element at a time and only as far as its answer: a `Where` then a `Select`
interleave, `Any`, `First` and `Take` stop where they answer, and a query made before an `Add` reads the
added element (#685). And a list changed while a `foreach`, a query or `List.ForEach` walks it throws
.NET's `InvalidOperationException`, where the walk went on, and a loop that added as it went never ended
(#863).

**Where they go.** The LINQ walker, `utils/query.ts`, is about 1.9 KB of the 3,714 (1,990 gzipped on its
own). `List<T>`'s version is the rest, about 1.8 KB: its module, `utils/list-version.ts` (757 on its own),
and the 754 call sites the runtime's transpiled components now write through it (398 `listAdd`, 146
`listWalk`, 105 `sequenceWalk`, 64 `listClear` and 41 `listSet`), which cost 1,076 bytes against the inline
forms they replaced, measured by writing them back and bundling again.

**Why not smaller.** Three cheaper shapes were weighed, and none keeps .NET's semantics for less:

- *The version paid only by the lists that are enumerated.* Only the writes can move. A walk is the
  enumeration itself, so its 251 call sites stay, and an element written through the indexer cannot be
  seen on an array without a Proxy, so `listSet` stays. Writing `Add` and `Clear` back inline (`push`,
  `splice(0)`) and letting a list's first walk install the version on that list saves 459 bytes, measured,
  before the installer's own code is paid back, and it gives every walked list an own `push` property, a
  change of shape on the runtime's busiest arrays. About 0.1% of the runtime, for that risk: not taken.
- *The walker in a module only the pages that use LINQ load.* Nine of the runtime's own components use
  it, `Dialog` and `Select` among them, and `seq` recognizes a query for every LINQ helper, so nearly
  every page would load that module. It would save nothing.
- *A walker that walks one way.* Each operator walks pulled, by a generator, for a `foreach`, and pushed,
  a call per element, for `ToList`, `Count`, `First` and the others. A chain of generators cost five to
  nine times what the array methods did, measured when the walker was written, so the pushed half is what
  keeps a query's cost near theirs.

#1620 is measured against `main` at 84e9b1381. Its 1,557 bytes are `utils/calendar.ts` (1,629 gzipped on its
own, the Gregorian arithmetic `datetime.ts` already carried included) and the reader's and the writer's
calls into it. The calendars' data is not in the runtime: a culture's format data carries its own
calendar, measured by the server from .NET's (`CalendarBridge`), so only a page in a Persian or an Um Al
Qura culture pays for that calendar's months: 385 bytes gzipped of fa-IR's data and 516 of ar-SA's. The
Persian calendar's arithmetic in .NET computes each year from the equinox; carried in the runtime, every
page would pay for it.

## Since the record of #1852

`main` grew 3,907 bytes past the base and its deltas from #1852's record to f79bec9da without a change recording
them: each merge stayed inside the ceiling of its day, floor(390,447 × 1.01) = 394,351 before #1884's delta and
floor(395,786 × 1.01) = 399,743 after it, until #1913, whose own 106 bytes met the second (#1922). #1852's merge (187ac4540) measures 390,447, the base and its own delta exactly, so the
rows start there, measured as this page's tables are, at every first-parent merge that touched a source of the bundle.
The method reproduces `main` at f79bec9da byte for byte: 399,693. #1880 and #1901 changed no byte of it.

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1871 | b7490b4fb | 390,447 | 390,955 | +508 | +2,212 | a month grid stops at its calendar's range | yes |
| #1879 | 5d6ec6a1c | 390,955 | 391,019 | +64 | +324 | a pager speaks the reader's language | yes |
| #1891 | 4ddb84f60 | 391,019 | 391,611 | +592 | +1,808 | uploads hold many files at once and several instances of an app | yes |
| #1888 | a53d40924 | 391,611 | 391,673 | +62 | +232 | a table pan's click guard leaves with its click or its table | yes |
| #1895 | 2dda360cc | 391,673 | 393,360 | +1,687 | +7,177 | a component builds its child against the box its parent gives it | yes |
| #1884 | 3187a51df | 393,360 | 398,769 | +5,409 | +21,893 | a gesture of the app's own is recognized, in one arena with the SDK's | yes |
| #1896 | f9b004ca6 | 398,769 | 398,836 | +67 | -111 | a hold's, a drag's, a lost press's and a pan's click guards are one owned guard | yes |
| #1902 | 90f1d61fa | 398,836 | 399,183 | +347 | +1,065 | content takes the look its component hands down, and a string is a node | yes |
| #1908 | 96323817f | 399,183 | 399,202 | +19 | +91 | Photon fills an even-odd shape by its rule, as the web does | yes |
| #1917 | 525c9bf3f | 399,202 | 399,693 | +491 | +2,571 | inputs and pickers take a style of their own, and builders for what they repeat | yes |
| **all** | | **390,447** | **399,693** | **+9,246** | **+37,262** | | |

#1884 grew 5,409 bytes and recorded 5,339, so 70 of its bytes are among the 3,907. The rest is the nine other merges'
growth, and the 3,907 are recorded as an entry of their own, `Budgets/served-runtime/main-growth-to-f79bec9da.json`,
rather than inside the delta of the change that met the ceiling, so each delta says what its own change bought.

Two older facts the same measurement shows. #1852's record held growth that was not its own: its 4,530 bytes, measured
against the base, took in the 3,685 that the merges before it had grown past the base, since its own merge grew the
bundle by 845. And 0.2.0-preview.72 (a27703d6a), whose release folded the base of 385,917, already measured 388,072,
2,155 bytes over it.

## The record of #1913

| Pull request | Merge | Before | After | Gzip | Raw | What it delivered | In proportion |
|---|---|---:|---:|---:|---:|---|---|
| #1913 | e4f1de44e | 399,693 | 399,799 | +106 | +301 | a static generic class keeps its statics per closed type | yes |

#1913 is measured against `main` at f79bec9da, which stood 50 bytes under the ceiling of the base and its two
deltas, floor(395,786 × 1.01) = 399,743. Its 106 bytes are `statics` in `utils/closures.ts`, the object it makes
once per closed type of a static generic class, which keeps that type's statics and its type initializer apart
from every other closed type's, as .NET does (#1855). They are the helper's least shape: a mark of the closed type
on that object, which nothing reads, was dropped to reach them. It records its own 106 bytes as its delta. The 3,907
bytes `main` had grown past the base and its deltas without crossing the ceiling are other pull requests' growth,
recorded as the entry of their own above.
