# Tasks

## 1. The push loop and its check

- [ ] 1.1 Write `scripts/publish-packages.sh <packages-dir> <version> [package-id ...]`: skip a package the index lists at that version, pause between pushes, try again after waits of 30, 60, 120 and 240 seconds, stop at once on the key's refusal, go on after a package that never goes through and fail at the end naming it, refuse an unknown id before any push. Waits, pause and index base read from the environment. Verify it runs under macOS's `/bin/bash` 3.2 and under bash 5.
- [ ] 1.2 Write `scripts/check-publish-packages.sh`, which runs the real script against a stub `dotnet` and a `file://` index and asserts each requirement of `specs/release-publishing/spec.md` from the stub's record of calls. Verify it passes, and that it fails, naming the broken requirement, against each wrong version of the loop: one that stops at the first failure, one that pushes what the index lists, and one that tries a refused key again.
- [ ] 1.3 Add the `publish-packages` job to `ci.yml` (`ubuntu-latest`, on every pull request and on `main`) running the check. Verify the job runs with steps in this pull request's CI.

## 2. The tag's pipeline runs the scripts

- [ ] 2.1 Lift `release-github`'s step into `scripts/release-page.sh <tag>` unchanged in what it does, and make `release-github` run it. Verify by running it against `v0.2.0-preview.61`, whose page exists: it edits that page and its body stays byte for byte the annotation's.
- [ ] 2.2 Make `publish-nuget` check out the repository and run `scripts/publish-packages.sh nupkgs <the tag's version>`. Verify by reading the job against the script's usage, since the job runs only on a tag: the next release's log is its proof, and the PR body says so.

## 3. The release finished by hand

- [ ] 3.1 Add `.github/workflows/publish-release.yml`: `workflow_dispatch` with `run-id`, `tag` and `packages`, the inputs passed to the shell only through `env:`, the run's `nupkgs` downloaded with `actions/download-artifact@v4`, the push script, then the release page script. Verify with `actionlint` where it is available, and by running it once this merges: run 38132878034 and tag `v0.2.0-preview.61`, which finds every package listed, pushes nothing and leaves the release page as it is.
- [ ] 3.2 Say in CLAUDE.md's Version Management section what to do when `publish-nuget` stops halfway: no new version, run `Publish a release` with the tag run's id and the tag. No wiki page describes the release pipeline, so the wiki does not change. Verify the docs guards pass (`dotnet test tests/eQuantic.UI.Web.Tests`).

## 4. The record

- [ ] 4.1 Add the `0.2.0-preview.61` release line to `docs/LEDGER.md` (the line drafted for it, naming its 45 pull requests and #756) and this change's own line citing #759. Verify the docs index guard passes.
- [ ] 4.2 Validate and archive the change (`./scripts/check-openspec.sh`, then `openspec archive a-failed-push-is-tried-again --yes`) before the merge, and verify `openspec/specs/release-publishing/spec.md` exists with its Purpose.
