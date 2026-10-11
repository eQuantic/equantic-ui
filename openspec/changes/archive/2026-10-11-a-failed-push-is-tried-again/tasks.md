# Tasks

## 1. The push loop and its check

- [x] 1.1 Write `scripts/publish-packages.sh <packages-dir> <version> [package-id ...]`: skip a package the index lists at that version, pause between pushes, try again in rounds over what failed, after waits of 30, 60, 120 and 240 seconds spent once for the whole list, not try again a push refused for the key, go on after a package that never goes through or is refused and fail at the end naming each, refuse an unknown id or a package at another version before any push. Waits, pause and index base read from the environment. Verify it runs under macOS's `/bin/bash` 3.2, the shell of the runner that publishes; the CI job runs it under ubuntu's bash 5.
- [x] 1.2 Write `scripts/check-publish-packages.sh`, which runs the real script against a stub `dotnet` and a `file://` index and asserts each requirement of `specs/release-publishing/spec.md` from the stub's record of calls. Verify it passes, and that it fails, naming the broken requirement, against each wrong version of the loop: one that stops at the first failure, one that pushes what the index lists, one that tries a refused key again, and five more, each a control inside the check.
- [x] 1.3 Add the `publish-packages` job to `ci.yml` (`ubuntu-latest`, on every pull request and on `main`) running the check. Verify the workflow parses with the job in it and the check passes locally; this pull request's CI is the job's first run, and the PR body records it.

## 2. The tag's pipeline runs the scripts

- [x] 2.1 Lift `release-github`'s step into `scripts/release-page.sh <tag>` unchanged in what it does, and make `release-github` run it. Verify by running it against `v0.2.0-preview.61`, whose page exists: it edits that page and its body stays byte for byte the annotation's.
- [x] 2.2 Make `publish-nuget` check out the repository and run `scripts/publish-packages.sh nupkgs <the tag's version>`. Verify by reading the job against the script's usage, since the job runs only on a tag: the next release's log is its proof, and the PR body says so.

## 3. The release finished by hand

- [x] 3.1 Add `.github/workflows/publish-release.yml`: `workflow_dispatch` with `run-id`, `tag` and `packages`, the inputs passed to the shell only through `env:`, a run that is not the push of the tag refused, the run's `nupkgs` downloaded with `actions/download-artifact@v4`, the push script, then the release page script. Verify its publish step against a stub with good and hostile inputs, and its steps for real before the merge, since a manual workflow can be dispatched only once it is on `main`: a throwaway branch carries the same file with a push trigger and the inputs written in. With the tag's own run (38132878034, tag `v0.2.0-preview.61`), run 38156657270 accepts the run, finds every package listed, pushes nothing and leaves the release page as it is. With a pull request's run, run 38156739483 stops before the download.
- [x] 3.2 Say in CLAUDE.md's Version Management section what to do when `publish-nuget` stops halfway: no new version, run `Publish a release` with the tag run's id and the tag. No wiki page describes the release pipeline, so the wiki does not change. Verify the docs guards pass (`dotnet test tests/eQuantic.UI.Web.Tests`).

## 4. The record

- [x] 4.1 Add the `0.2.0-preview.61` release line to `docs/LEDGER.md` (the line drafted for it, naming its 45 pull requests and #756) and this change's own line citing #759. Verify the docs index guard passes.
- [x] 4.2 Validate and archive the change (`./scripts/check-openspec.sh`, then `openspec archive a-failed-push-is-tried-again --yes`) before the merge, and verify `openspec/specs/release-publishing/spec.md` exists with its Purpose.
