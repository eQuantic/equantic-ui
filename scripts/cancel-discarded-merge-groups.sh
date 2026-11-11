#!/usr/bin/env bash
# The runs of the merge groups the queue discarded, cancelled (#1446).
#
#   scripts/cancel-discarded-merge-groups.sh              cancel them
#   scripts/cancel-discarded-merge-groups.sh --dry-run    name them, cancel nothing
#   scripts/cancel-discarded-merge-groups.sh --self-test  prove every path against a stub of gh
#
# GitHub's merge queue discards a group when an entry ahead of it fails or leaves, and builds new groups
# behind it. It does not cancel the discarded group's run. Each group's ref is its own
# (gh-readonly-queue/main/pr-N-<sha>), so the workflow's concurrency never groups that run with the one
# that replaced it, and its macOS jobs kept waiting for the plan's five runners beside the live groups'.
# On 2026-11-11, 11 such runs were queued from 15:42Z on, and #1378 fell out of the queue twice by
# timeout without its macOS jobs ever starting.
#
# What tells a discarded group apart is its ref: the queue deletes it. A group the queue still holds
# keeps its ref, so its run is never touched. A lookup that fails any other way than a 404 cancels
# nothing and fails the sweep, because a guess here would cancel a group that is going to merge. The
# run that sweeps is among the runs it reads, so a run whose own group is gone cancels itself.
#
# GITHUB_REPOSITORY  owner/name of the repository (set by Actions)
# GH_TOKEN           a token that may cancel runs (actions: write)
# EQ_WORKFLOW        the workflow whose runs are swept; ci.yml by default
set -euo pipefail

sweep() {
  local dry_run=$1
  local repo=${GITHUB_REPOSITORY:?GITHUB_REPOSITORY names the repository}
  local workflow=${EQ_WORKFLOW:-ci.yml}
  local status runs id branch sha out own=""
  local -a gone=()
  local kept=0 merged=0 cancelled=0 failures=0

  # A run waits in one of these until it ends; the API filters by one status a call.
  for status in queued in_progress waiting requested pending; do
    runs=$(gh api "repos/$repo/actions/workflows/$workflow/runs?event=merge_group&status=$status&per_page=100" \
      --jq '.workflow_runs[] | "\(.id) \(.head_branch) \(.head_sha)"')
    while read -r id branch sha; do
      [ -n "$id" ] || continue
      if out=$(gh api "repos/$repo/git/ref/heads/$branch" --jq .ref 2>&1); then
        kept=$((kept + 1))
        continue
      fi
      if [[ $out != *"HTTP 404"* ]]; then
        echo "::error::Run $id: its group $branch could not be looked up, so it is left running: $out" >&2
        failures=$((failures + 1))
        continue
      fi
      # The queue deletes a group's ref when it merges as well as when it discards it. A merged group's
      # commit is on main, and its run's last jobs are left to end.
      if out=$(gh api "repos/$repo/compare/$sha...main" --jq .status 2>&1); then
        if [ "$out" = "ahead" ] || [ "$out" = "identical" ]; then
          merged=$((merged + 1))
          continue
        fi
      else
        echo "::error::Run $id: whether its group $branch merged could not be told, so it is left running: $out" >&2
        failures=$((failures + 1))
        continue
      fi

      echo "Run $id ($status): its group $branch is gone, and it did not merge."
      # The sweeping run's own is cancelled last: cancelling it ends this step.
      if [ "$id" = "${GITHUB_RUN_ID:-}" ]; then own=$id; else gone+=("$id"); fi
    done <<< "$runs"
  done
  [ -z "$own" ] || { echo "Run $own is this run."; gone+=("$own"); }

  if [ "$dry_run" = 1 ]; then
    echo "Merge-group runs kept: $kept, merged: $merged, to cancel: ${#gone[@]}. A dry run: nothing was cancelled."
  else
    for id in ${gone[@]+"${gone[@]}"}; do
      if out=$(gh api -X POST "repos/$repo/actions/runs/$id/cancel" 2>&1); then
        cancelled=$((cancelled + 1))
      elif [[ $out == *"HTTP 409"* ]]; then
        # It ended between the listing and the cancel: nothing left to stop.
        echo "Run $id had already ended."
      else
        echo "::error::Run $id could not be cancelled: $out" >&2
        failures=$((failures + 1))
      fi
    done
    echo "Merge-group runs kept: $kept, merged: $merged, cancelled: $cancelled."
  fi
  [ "$failures" -eq 0 ] || { echo "::error::$failures merge-group run(s) were left unresolved." >&2; return 1; }
}

# ---- the self-test ---------------------------------------------------------------------------------
# A stub of gh answers from files: the runs listed per status, the refs that exist, the lookups that
# fail with a 500, the group commits already on main, and the cancels that answer 409 or 403. It records every cancel. Each case states the
# exact set it must cancel, so a sweep that cancels everything, or nothing, fails one.

self_test() {
  local work runs=0 failures=0
  work=$(mktemp -d)
  trap 'rm -rf "$work"' RETURN
  mkdir -p "$work/bin"
  cat > "$work/bin/gh" <<'STUB'
#!/usr/bin/env bash
set -u
stub=$EQ_STUB
if [ "$1 $2" = "api -X" ]; then
  id=${4#repos/*/actions/runs/}; id=${id%/cancel}
  echo "$id" >> "$stub/cancelled"
  grep -qx "$id" "$stub/finished" 2>/dev/null && { echo "gh: Cannot cancel a completed run (HTTP 409)" >&2; exit 1; }
  grep -qx "$id" "$stub/forbidden" 2>/dev/null && { echo "gh: Resource not accessible by integration (HTTP 403)" >&2; exit 1; }
  exit 0
fi
case "$2" in
  repos/*/actions/workflows/*/runs\?*)
    status=${2##*status=}; status=${status%%&*}
    cat "$stub/runs-$status" 2>/dev/null
    exit 0 ;;
  repos/*/compare/*)
    sha=${2#repos/*/compare/}; sha=${sha%...main}
    grep -qx "$sha" "$stub/merged" 2>/dev/null && { echo "ahead"; exit 0; }
    grep -qx "$sha" "$stub/compare-errors" 2>/dev/null && { echo "gh: Server Error (HTTP 502)" >&2; exit 1; }
    echo "diverged"; exit 0 ;;
  repos/*/git/ref/heads/*)
    branch=${2#repos/*/git/ref/heads/}
    grep -qx "$branch" "$stub/refs" 2>/dev/null && { echo "refs/heads/$branch"; exit 0; }
    grep -qx "$branch" "$stub/errors" 2>/dev/null && { echo "gh: Server Error (HTTP 500)" >&2; exit 1; }
    echo "gh: Not Found (HTTP 404)" >&2; exit 1 ;;
esac
echo "stub: unexpected call: $*" >&2
exit 3
STUB
  chmod +x "$work/bin/gh"

  # case <name> <expected exit> <expected cancels, space-separated> [--dry-run]; the fixture is $work/case
  check() {
    local name=$1 want_exit=$2 want=$3 mode=${4:-}
    local got_exit=0 got
    runs=$((runs + 1))
    PATH="$work/bin:$PATH" EQ_STUB="$work/case" GITHUB_REPOSITORY="eQuantic/equantic-ui" GITHUB_RUN_ID=13 \
      bash "$self" ${mode:+"$mode"} > "$work/out" 2>&1 || got_exit=$?
    got=$({ cat "$work/case/cancelled" 2>/dev/null || true; } | tr '\n' ' ' | sed 's/ $//')
    if [ "$got_exit" != "$want_exit" ] || [ "$got" != "$want" ]; then
      echo "FAIL $name: exit $got_exit (want $want_exit), cancelled [$got] (want [$want])" >&2
      sed 's/^/  | /' "$work/out" >&2
      failures=$((failures + 1))
    else
      echo "ok   $name"
    fi
  }
  fixture() {
    rm -rf "$work/case"; mkdir -p "$work/case"
  }

  # A live group keeps its run; two discarded ones are cancelled, the sweeping run's own among them.
  fixture
  # This run (13) is listed before another discarded one (15), and is cancelled after it.
  printf '11 gh-readonly-queue/main/pr-1-live a11\n12 gh-readonly-queue/main/pr-2-gone a12\n' > "$work/case/runs-queued"
  printf '13 gh-readonly-queue/main/pr-3-gone a13\n' > "$work/case/runs-in_progress"
  printf '14 gh-readonly-queue/main/pr-4-live a14\n15 gh-readonly-queue/main/pr-5-gone a15\n' > "$work/case/runs-waiting"
  printf '16 gh-readonly-queue/main/pr-6-merged a16\n' > "$work/case/runs-pending"
  printf 'gh-readonly-queue/main/pr-1-live\ngh-readonly-queue/main/pr-4-live\n' > "$work/case/refs"
  printf 'a16\n' > "$work/case/merged"
  check "the live and the merged groups run on, the discarded ones stop, this run last" 0 "12 15 13"
  grep -q "Run 13 is this run" "$work/out" || { echo "FAIL the log names the run that cancels itself" >&2; failures=$((failures + 1)); }

  # The same queue in a dry run: named, not cancelled.
  rm -f "$work/case/cancelled"
  check "a dry run cancels nothing" 0 "" --dry-run

  # A lookup that fails with a 500 is no proof the group is gone: that run is left, the rest swept, and
  # the sweep fails so the failure is seen.
  fixture
  printf '21 gh-readonly-queue/main/pr-5-unknown b21\n22 gh-readonly-queue/main/pr-6-gone b22\n' > "$work/case/runs-queued"
  printf 'gh-readonly-queue/main/pr-5-unknown\n' > "$work/case/errors"
  : > "$work/case/refs"
  check "an uncertain lookup cancels nothing and fails" 1 "22"

  # A run that ended between the listing and the cancel is not a failure.
  fixture
  printf '31 gh-readonly-queue/main/pr-7-gone c31\n' > "$work/case/runs-queued"
  : > "$work/case/refs"
  printf '31\n' > "$work/case/finished"
  check "a run that already ended is fine" 0 "31"

  # A token that may not cancel is a misconfiguration, said out loud.
  fixture
  printf '41 gh-readonly-queue/main/pr-8-gone d41\n' > "$work/case/runs-queued"
  : > "$work/case/refs"
  printf '41\n' > "$work/case/forbidden"
  check "a cancel the token may not make fails" 1 "41"

  # Whether a gone group merged is a lookup too: one that fails cancels nothing.
  fixture
  printf '51 gh-readonly-queue/main/pr-9-gone e51\n' > "$work/case/runs-queued"
  : > "$work/case/refs"
  printf 'e51\n' > "$work/case/compare-errors"
  check "an uncertain merge cancels nothing and fails" 1 ""

  # An empty queue is a clean sweep.
  fixture
  check "nothing to sweep" 0 ""

  if [ "$failures" -gt 0 ]; then
    echo "::error::The merge-group sweep's self-test failed $failures of $runs checks." >&2
    return 1
  fi
  echo "The merge-group sweep's self-test passed all $runs checks."
}

self=$(cd "$(dirname "$0")" && pwd)/$(basename "$0")
case "${1:-}" in
  --self-test) self_test ;;
  --dry-run) sweep 1 ;;
  "") sweep 0 ;;
  *) echo "usage: $0 [--dry-run | --self-test]" >&2; exit 2 ;;
esac
