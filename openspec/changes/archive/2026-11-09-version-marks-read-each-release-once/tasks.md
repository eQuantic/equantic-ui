# Tasks

## 1. Measured

- [x] 1.1 Count the marks and read how CI checks out. Verify: 159 marks, 154 distinct searches in 26 scopes, and a test job that checks out at depth one and fetches 215 branches and 67 tags at depth one
- [x] 1.2 Time one `git log -G` walk over the tags with every search in one alternation, on a checkout shaped like CI's. Verify: over two minutes, the regular expression over every line of every root's tree
- [x] 1.3 Time the release-to-release diffs without a filter. Verify: three seconds for all 67, 155 MB in all, 29 MB for the largest

## 2. One reading of the releases

- [x] 2.1 Write `FirstReleases` and point the guard at it. Verify: the guard passes in six seconds on a laptop
- [x] 2.2 Compare its answers with the old searches', on a full clone and on a checkout shaped like CI's. Verify: all 154 alike on CI's shape, and all but `string? Href { get; init; }` on the full clone, a name no release tree held
- [x] 2.3 Add `FirstReleasesTests`. Verify: each of four breaks of the reader (lexical order, rename detection, the tab git ends a spaced name with, a header read inside a hunk) fails the case that names it
- [x] 2.4 Make one mark of a copy of the wiki wrong and point `EQ_WIKI_DIR` at it. Verify: the guard fails naming the page, the symbol, the claim and the release

## 3. CI

- [x] 3.1 Fetch only the release tags, and pass `--blame-hang-timeout 15m` on both `dotnet test` lines. Verify: the slowest case of main's last run besides this one took 55 seconds, and the pull request's `test` jobs pass

## 4. Proved against the real thing

- [x] 4.1 Run the Web suite whole through the suite lock. Verify: its total, duration and exit code

## 5. Documentation

- [x] 5.1 One entry for the ledger citing #999, as a file of its own under `docs/ledger/`
