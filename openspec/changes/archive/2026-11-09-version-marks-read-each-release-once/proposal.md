# Proposal

Closes #999, a Task under #849.

## Why

`EveryVersionMark_NamesTheReleaseItActuallyShippedIn` asked git once per version mark of the wiki: a
pickaxe search (`git log --all -S<symbol>`) over every ref, then `git tag --contains` for the
commits it found. CI's test jobs check out one commit and fetch every branch and tag at depth one,
so each of those refs is a root whose diff is its whole tree, and every search read about three
hundred trees. With 159 marks the case took 34 to 39 minutes on CI's runners, and after
`0.2.0-preview.67` it went past the 60 minutes CI's `--blame-hang-timeout` gives a case: the Web
suite's host was aborted on Linux and on Windows, on main's own run (42248241291) as on every pull
request since. CI's hang timeout sat at 60 minutes only for this case, so a real hang took an hour to
fail.

## What Changes

- `FirstReleases`, beside the guard in the Web tests, answers every mark in one reading of the
  releases. Each release tag is compared with the one before it, the first with the empty tree, by
  `git diff --no-renames --unified=0`, and every search is counted in the same pass: its count in a
  release is its count in the release before plus what the diff added, less what it removed, and the
  first release where the count is above zero is the one it shipped in. Sixty-seven diffs answer
  every mark; a `text @ path` search counts the text only in that file or under that directory.
- The answer reads only the releases' trees, never the history between them, so CI's shallow
  checkout and a full clone answer the same. Measured on all 154 distinct searches the wiki writes:
  the same answer as the old searches gave on a checkout shaped like CI's, every one, and as they
  gave on a full clone, every one but `string? Href { get; init; }`, which was added and renamed on
  the same day and never reached a release tree (the old search answered the release that held the
  commits, the new one answers none, and neither fails the mark).
- `FirstReleasesTests` writes a repository with three releases and pins what the guard must get
  right by construction: preview.9 before preview.10, a path that narrows the count, a file moved
  into the path, a line inside a hunk that reads like a file's header, a name with a space, a symbol
  added and removed between releases, and a checkout of each release at depth one answering as the
  full history does.
- CI fetches the release tags alone (`+refs/tags/v0.*:refs/tags/v0.*`, depth one), not every branch
  too, which nothing reads; and `--blame-hang-timeout` is 15 minutes again on both `dotnet test`
  lines, where the slowest other case took 55 seconds on main's last run.
- Nothing a developer using the SDK writes or meets changes, and neither the public surface nor the
  developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the test suites and CI alone, and no requirement of the product moves, so
the change sets `skip_specs`.)
