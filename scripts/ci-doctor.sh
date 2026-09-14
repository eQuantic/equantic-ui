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
# Usage: scripts/ci-doctor.sh [commit-ish]   (default: HEAD)
set -euo pipefail

REPO="${EQ_REPO:-eQuantic/equantic-ui}"
# The COMMIT, not the branch. `gh run list --branch X | .[0]` is the newest run ON that branch, so a
# push that produced no run at all reports the PREVIOUS push's run and this script answers ok —
# which is precisely the failure it exists to catch, committed inside it. Found in review.
SHA="$(git rev-parse "${1:-HEAD}")"

name=$(gh api "repos/$REPO/actions/workflows" \
  --jq '.workflows[] | select(.path == ".github/workflows/ci.yml") | .name')

if [ "$name" != "CI" ]; then
  echo "BROKEN: GitHub calls the workflow '$name', not 'CI'."
  echo "  It could not read .github/workflows/ci.yml, so no run will ever create a job."
  echo "  Look for an expression that does not parse — a '#' inside an 'if: |' block is text,"
  echo "  not a comment, and that is what did it the first time."
  exit 1
fi

# Asked through the API rather than `gh run list`, which resolves the repository from the checkout
# and takes no --repo here: EQ_REPO would otherwise check one repository's workflow name against
# another repository's run. Found in review.
run=$(gh api "repos/$REPO/actions/runs?head_sha=$SHA" \
  --jq '[.workflow_runs[] | select(.path == ".github/workflows/ci.yml")] | sort_by(.run_number) | last | .id // empty')

if [ -z "$run" ]; then
  echo "NO RUN: the CI workflow has never run for $SHA. Push it, or check the trigger."
  exit 1
fi

count=$(gh api "repos/$REPO/actions/runs/$run/jobs" --jq '.jobs | length')
if [ "$count" -eq 0 ]; then
  echo "CI DID NOT RUN: run $run exists for $SHA and created ZERO jobs."
  echo "  That is a startup failure. A PR on this commit can still read CLEAN — the ruleset requires"
  echo "  a review, not a status check — so nothing else will tell you."
  exit 1
fi

echo "ok: workflow is 'CI'; run $run on $SHA created $count jobs."
