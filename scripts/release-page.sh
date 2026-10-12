#!/usr/bin/env bash
# Writes the GitHub release page of a framework tag from the tag's own annotation (#759).
#
# The migration notes live in the `git tag -a` message, so the page is read from the tag object
# rather than kept as a second copy that could disagree with it. The tag's pipeline writes the page
# after nuget.org has the packages (release-github), and the workflow run by hand writes it when it
# finishes a release whose pipeline stopped before that job (publish-release.yml): one script, so
# the two cannot write it differently. A page that exists is edited, never created twice.
#
#   scripts/release-page.sh <tag>        e.g. v0.2.0-preview.61, in a checkout of the repository
#
# GH_TOKEN  a token that may write releases (`contents: write` in a workflow).
set -euo pipefail

[ $# -eq 1 ] || { echo "usage: release-page.sh <tag>" >&2; exit 2; }
tag=$1
# A tag can be text a person typed into the manual workflow: it is refused, never escaped.
case "$tag" in v[0-9]*) ;; *) echo "::error::'$tag' is not a framework tag (v<version>)." >&2; exit 2 ;; esac
case "$tag" in *[!A-Za-z0-9.+-]*) echo "::error::'$tag' is not a tag name." >&2; exit 2 ;; esac

notes=$(mktemp)
trap 'rm -f "$notes"' EXIT

# The checkout may hold the tag as a lightweight ref; the annotation lives on the tag OBJECT.
git fetch --force origin "+refs/tags/${tag}:refs/tags/${tag}"
git tag -l --format='%(contents:body)' "$tag" > "$notes"
if [ ! -s "$notes" ]; then
  echo "::warning::${tag} carries no release notes in its annotation; the release page says so."
  echo "_This tag was annotated without release notes._" > "$notes"
fi

version="${tag#v}"
prerelease=""
case "$version" in *-*) prerelease="--prerelease" ;; esac

if gh release view "$tag" >/dev/null 2>&1; then
  gh release edit "$tag" --title "eQuantic.UI $version" --notes-file "$notes" $prerelease
else
  gh release create "$tag" --verify-tag --title "eQuantic.UI $version" --notes-file "$notes" $prerelease
fi
