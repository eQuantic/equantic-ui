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

# BOTH questions are asked, always. An earlier version exited here, which made the script answer
# the per-commit question WITHOUT asking it — and got it wrong in exactly the case it exists for:
# while the fix lives on a branch, the registered name still comes from the default branch, so this
# said "no run will ever create a job" about a commit whose run was creating them as it spoke.
# The prose above had the scopes right and the control flow did not. Found by running it.
broken_name=0
if [ "$name" != "CI" ]; then
  broken_name=1
  echo "BROKEN NAME: GitHub calls the workflow '$name', not 'CI'."
  echo "  It could not read .github/workflows/ci.yml on the DEFAULT BRANCH, where this name comes"
  echo "  from. Look for an expression that does not parse — a '#' inside an 'if: |' block is text,"
  echo "  not a comment, and that is what did it the first time."
  echo "  This says nothing about $SHA on its own; the job count below is what does."
fi

# Asked through the API rather than `gh run list`, which resolves the repository from the checkout
# and takes no --repo here: EQ_REPO would otherwise check one repository's workflow name against
# another repository's run. Found in review.
# `last` here is jq's last/0 — `.[-1]` on the array piped in — and NOT last/1, the stream form.
# jq defines both, `builtins` lists them side by side, and a reader cannot tell them apart at a
# glance; raised in review as a bug and settled by running it:
#   echo '{"workflow_runs":[{"path":".github/workflows/ci.yml","run_number":3,"id":333},
#                           {"path":".github/workflows/ci.yml","run_number":1,"id":111}]}' \
#     | jq '[.workflow_runs[]|select(.path==".github/workflows/ci.yml")]|sort_by(.run_number)|last|.id'
#   333
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

if [ "$broken_name" -eq 1 ]; then
  echo "ok HERE, BROKEN THERE: run $run on $SHA created $count jobs, so the workflow on this commit"
  echo "  is fine — and the repository still registers the default branch's broken copy. That is"
  echo "  what a fix looks like before it merges. Merge it, then run this again on main."
  exit 1
fi

echo "ok: workflow is 'CI'; run $run on $SHA created $count jobs."
