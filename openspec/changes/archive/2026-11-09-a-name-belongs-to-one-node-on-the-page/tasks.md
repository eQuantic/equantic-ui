# Tasks

## 1. Measured on main

- [x] 1.1 Host the dashboard from its own `Program` and count each page's ids. Verify: 21 routes, no id drawn twice on any of them
- [x] 1.2 Collect the in-page links of every route. Verify: none, so the fragment test fails finding nothing to check

## 2. The guard

- [x] 2.1 Put the page of the dashboard's shell in two arms of one `AdaptiveNode`. Verify: `NoNameIsClaimedTwiceOnAPage` fails naming the routes and the ids, and passes once the shell is back
- [x] 2.2 Run the Server suite whole. Verify: its total, duration and exit code, with the sample referenced

## 3. The first fragment a sample links to

- [x] 3.1 Give /charts a "Jump to" row and its cards their bookmarks. Verify: `NoFragmentPointsAtAPlaceThatIsNotThere` passes, and fails when a card's bookmark is renamed
- [x] 3.2 Drive /charts in a browser at a phone's width and a desktop's. Verify: each link moves the page to its card, each id is in the document once, and the hydrated classes are the served ones

## 4. The rail scrolls its destinations

- [x] 4.1 Measure the document at 1280x900 on three routes. Verify: 1034px tall before the fix on /, /rows and /charts alike, and the rail is what overflows
- [x] 4.2 Give the destinations the rail's remaining share, in a scroller, inset 4dp. Verify: the document is 900px, a link to a fragment leaves the window at 0, every destination has 4px of room inside the scroller's clip on each side and the last one below it, and the hydrated classes are the served ones
- [x] 4.3 Open the compact drawer on a 375x812 phone. Verify: its destinations scroll, the sandbox card and the account stay inside the window, and the document is 812px
- [x] 4.4 Take the README's four web pictures again and look at each beside main's. Verify: the dashboard, diff and forms pictures change only inside the rail, the charts picture also by its new row, and `ReadmeCapturesTests` passes with the sources recorded
- [x] 4.5 Run the Web suite whole, which compiles the shell for `ConsoleShellTests`. Verify: its total, duration and exit code

## 5. Documentation

- [x] 5.1 One entry for the ledger citing #1252, as a file of its own under `docs/ledger/`
