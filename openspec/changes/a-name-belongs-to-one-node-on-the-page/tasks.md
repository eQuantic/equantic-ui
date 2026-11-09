# Tasks

## 1. Measured on main

- [x] 1.1 Host the dashboard from its own `Program` and count each page's ids. Verify: 21 routes, no id drawn twice on any of them
- [x] 1.2 Collect the in-page links of every route. Verify: none, so the fragment test fails finding nothing to check

## 2. The guard

- [ ] 2.1 Put the page of the dashboard's shell in two arms of one `AdaptiveNode`. Verify: `NoNameIsClaimedTwiceOnAPage` fails naming the routes and the ids, and passes once the shell is back
- [ ] 2.2 Run the Server suite whole. Verify: its total, duration and exit code, with the sample referenced

## 3. The first fragment a sample links to

- [ ] 3.1 Give /charts a "Jump to" row and its cards their bookmarks. Verify: `NoFragmentPointsAtAPlaceThatIsNotThere` passes, and fails when a card's bookmark is renamed
- [ ] 3.2 Drive /charts in a browser at a phone's width and a desktop's. Verify: each link moves the page to its card, each id is in the document once, and the hydrated classes are the served ones
- [ ] 3.3 Take the README's charts picture again and look at it. Verify: `ReadmeCapturesTests` passes with the new sources recorded

## 4. Documentation

- [ ] 4.1 One entry for the ledger citing #1252, as a file of its own under `docs/ledger/`
