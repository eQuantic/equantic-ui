# Design

## Context

`publish-nuget` (in `.github/workflows/ci.yml`) runs only on a `v*` tag, on `macos-latest`, after
`gate-templates`. It downloads the run's `nupkgs` artifact and runs an inline loop of
`dotnet nuget push --skip-duplicate`, one per package, under `set -euo pipefail`. The client tries a
failed `PUT` three times within seconds and has no option to wait longer or to go on with a list.
`release-github` needs `publish-nuget` and writes the release page from the tag's annotation with an
inline script. A rerun of a tag's failed jobs replays the workflow as it was at the tag, so a fix in
`ci.yml` reaches the next release and never the one that stopped. What happened on 2026-10-11 is the
proposal's Why. The requirements are in `specs/release-publishing/spec.md`.

## Goals / Non-Goals

**Goals:**
- One copy of the push loop and one of the release page, each run by the tag's pipeline and by the
  manual workflow alike.
- A check that runs the push loop on every pull request, since the paths that publish run only on a
  tag or by hand.

**Non-Goals:**
- GitHub Packages (`publish-github`): it pushes every package with `--skip-duplicate` and has not
  failed. It keeps its loop.
- Adding the new check job to the ruleset's required checks: that is a repository setting, the
  owner's to change.
- Moving `publish-nuget` off `macos-latest`: nothing measured asks for it.

## Decisions

**The loop is a script in `scripts/`, not a reusable workflow or a composite action.** It is the
shape the repository already proves its instruments with (`check-session-start.sh`,
`check-commit-messages.sh`): a script a stub can run on any machine. A reusable workflow or an action
would still need nuget.org to run at all.

**What nuget.org already has is read from its package index, not learned from a 409.** The script
reads `https://api.nuget.org/v3-flatcontainer/<id>/index.json`, the index a restore reads, and skips
a package listed at the release's version. On 2026-10-11 the duplicate path itself answered 500 five
times, so a loop that discovers duplicates by pushing them can die before it reaches a package still
owed. A package pushed a moment ago and not listed yet is pushed again and answers 409, which
`--skip-duplicate` counts as pushed. The index's base URL comes from the environment, so the check
can point it at files.

**A push refused for the key is not tried again, and the list still goes on. Every other failure is
tried again.** Waiting does not change a 401 or a 403 (the gallery's own sentence, "The specified API
key is invalid, has expired, or does not have permission", and the client's `Forbidden`/`Unauthorized`
are what the script reads), so the package is named as refused and the loop moves on. Stopping at
once was the first design, and the review of this change turned it down: nuget.org also answers 403
to one package the key's scope does not cover, as the first release met with a key scoped to new
versions only, and the packages after it would then have gone unpublished although they are
accepted. An expired key costs one fast request per package. A 5xx, a timeout and a dropped
connection are tried again. Retrying only on a 5xx was considered and rejected: a timeout has
no status. The cost is that a package nuget.org rejects as invalid (400) is tried again a few times
before the loop names it at the end.

**A package at another version fails the publish, and a version is compared as the index writes
it.** The loop this replaces pushed every `.nupkg` of the artifact. Taking only the ones at the
release's version would leave any other out without a word, so the script refuses such a directory
before it pushes anything. The index lists a version lower-cased and without its build metadata, and
the script compares it that way, or a version with a capital letter would never be found listed.

**The manual workflow refuses a run that is not the tag's own.** It asks GitHub for the run and
requires the push of that tag at the tag's commit. A pull request's run or main's packs the same
version from another commit, and nuget.org never takes a version back.

**Waits of 30, 60, 120 and 240 seconds, and a 15-second pause between packages.** On 2026-10-11 the
one package that failed in the one-off run went through 30 seconds later. Seven and a half minutes
per package at worst keeps a stubborn outage inside the job's timeout, and the loop still names
whatever it could not push. Both are read from the environment, so the check runs with no waits.

**The manual workflow takes a run, a tag and optional package ids.** `workflow_dispatch` with the
inputs `run-id`, `tag` and `packages`. It downloads the run's `nupkgs` artifact with
`actions/download-artifact@v4` (`run-id`, the workflow's own token, `actions: read`), runs the
script, then runs the release page script for the tag (`contents: write`). The inputs reach the shell
only through `env:`, never written into a `run:` block, since a package id is text a person typed.
The version is the tag without its `v`, and the script refuses an id that has no package at that
version in the artifact before pushing anything. Re-tagging or cutting a new version was rejected:
nuget.org is immutable, and a version cut for an outage is a number nobody needed. Rerunning the tag's
job was rejected because it is what failed: it walks the list from the top with the workflow frozen
at the tag.

**The release page is one script, lifted from `release-github` as it is.** `scripts/release-page.sh
<tag>` fetches the tag object, writes `%(contents:body)` to the notes, titles the page
`eQuantic.UI <version>`, marks a version with a hyphen as a pre-release, and edits a page that exists
instead of creating a second one. `release-github` and the manual workflow both run it.

**The check stubs `dotnet` and the index, and runs the real script.** `scripts/check-publish-packages.sh`
builds a directory of empty `.nupkg` files, an index of `file://` JSON documents, and a `dotnet` on
`PATH` that records each call and answers from a per-package script of outcomes. It asserts each
requirement of the spec by reading the stub's record, not the script's exit code alone. A new CI job,
`publish-packages`, runs it on `ubuntu-latest` on every pull request and on `main`. The script is also
run locally under macOS's `/bin/bash` 3.2, the shell `publish-nuget` meets on its runner, so it uses
no bash 4 feature.

**Flutter has no counterpart.** Flutter's SDK does not reach developers through a package feed, so
FLUTTER-PARITY has no row here. The .NET mechanism kept is `dotnet nuget push --skip-duplicate`; the
script adds the waiting and the list-continuing it lacks.

## Risks / Trade-offs

- [The check proves the script, not nuget.org] → The same logic ran against nuget.org in run
  38137298726 and pushed 17 packages through a 500. The first release after the merge is the next
  real run, and its log is read for the waits it took.
- [The index lags right after a push] → The package is pushed again and answers 409, which
  `--skip-duplicate` counts as pushed.
- [A run id that is not the release's] → The workflow refuses it before the download, and a
  directory holding a package at another version is refused by the script before any push.
- [The manual workflow is run with a tag that has no release page yet, or one that exists] → The page
  script creates it or edits it, so a second run changes nothing.
- [A person runs the manual workflow while the tag's own run is still publishing] → Both skip what is
  listed and push with `--skip-duplicate`, so the worst case is a 409. CLAUDE.md says to run it only
  after the tag's `publish-nuget` has failed.
