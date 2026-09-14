#!/usr/bin/env bash
# Does GitHub think this repository has a CI workflow, and did it RUN?
#
# A workflow whose `if:` expression does not parse fails before any job is created: zero jobs, no
# log, and a PR that reads CLEAN because the ruleset requires a review and not a status check. The
# file is valid YAML throughout, the jobs are all present, `needs` resolves — there is nothing
# between a YAML parser and the runner that objects. It happened here for an hour, and seven PRs
# merged on local runs alone.
#
# The tell is the NAME. GitHub registers a workflow under its `name:`; when it cannot read the file
# it falls back to the path. So `.github/workflows/ci.yml` where `CI` should be means the workflow
# is broken, whatever the file looks like from here.
#
# The two checks have DIFFERENT scopes, and reading them as one would mislead: the registered name
# is repository-wide and follows the DEFAULT BRANCH, so a fix on a branch does not clear it until
# that branch merges. The job count is per-ref and is what says whether this particular push ran.
#
# Usage: scripts/ci-doctor.sh [ref]     (default: the current branch)
set -euo pipefail

REPO="${EQ_REPO:-eQuantic/equantic-ui}"
REF="${1:-$(git rev-parse --abbrev-ref HEAD)}"

name=$(gh api "repos/$REPO/actions/workflows" \
  --jq '.workflows[] | select(.path == ".github/workflows/ci.yml") | .name')

if [ "$name" != "CI" ]; then
  echo "BROKEN: GitHub calls the workflow '$name', not 'CI'."
  echo "  It could not read .github/workflows/ci.yml, so no run will ever create a job."
  echo "  Look for an expression that does not parse — a '#' inside an 'if: |' block is text,"
  echo "  not a comment, and that is what did it the first time."
  exit 1
fi

jobs=$(gh run list --branch "$REF" --workflow=ci.yml --limit 1 --json databaseId --jq '.[0].databaseId // empty')
if [ -z "$jobs" ]; then
  echo "NO RUN: the CI workflow has never run for '$REF'. Push, or check the trigger."
  exit 1
fi

count=$(gh api "repos/$REPO/actions/runs/$jobs/jobs" --jq '.jobs | length')
if [ "$count" -eq 0 ]; then
  echo "CI DID NOT RUN: run $jobs exists for '$REF' and created ZERO jobs."
  echo "  That is a startup failure. A PR on this ref can still read CLEAN — the ruleset requires a"
  echo "  review, not a status check — so nothing else will tell you."
  exit 1
fi

echo "ok: workflow is 'CI'; run $jobs on '$REF' created $count jobs."
