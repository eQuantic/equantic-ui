#!/usr/bin/env bash
# Clones the wiki the docs guards read (WikiDiagnosticsTests and its siblings in
# tests/eQuantic.UI.Web.Tests) beside this repository, at the pull request's own wiki branch when
# the wiki has one (#406).
#
# The guards compare a pull request's code with the wiki. Reading master for every run tied the two
# together when they cannot move together: a pull request that added a diagnostic failed its own
# guard until its rows were published, and once they were, every other pull request based on main
# failed the same guard from the other side until the first one merged. So a pull request that
# changes what the wiki must say carries its pages on a wiki branch named like its own branch, and
# CI reads that branch. Master moves when the pull request merges, and a run with no such branch (a
# pull request that changes no page, a push to main) reads master.
#
#   scripts/checkout-wiki.sh <destination>   clone into <destination>, which must not exist
#   scripts/checkout-wiki.sh --self-test     prove every path against a local fixture wiki
#
# EQ_WIKI_BRANCH  the pull request's head ref, empty outside a pull request. It is text someone else
#                 wrote, so it is only ever compared and passed as one argument, never interpolated
#                 into a command.
# EQ_WIKI_URL     the wiki's repository; the self-test points it at its fixture.
set -euo pipefail

readonly DEFAULT_URL="https://github.com/eQuantic/equantic-ui.wiki.git"

# Clones the wiki into $1, at EQ_WIKI_BRANCH when the wiki has a branch of exactly that name, and at
# its default branch otherwise, then says which one it read.
checkout() {
  local destination=$1
  local url=${EQ_WIKI_URL:-$DEFAULT_URL}
  local branch=${EQ_WIKI_BRANCH:-}
  local heads

  # The listing decides which branch is read, so a wiki that cannot be listed fails here rather
  # than falling back: master would be the wrong pages for a pull request that carries its own.
  if ! heads=$(git ls-remote --heads "$url"); then
    echo "::error::The wiki's branches could not be listed at $url." >&2
    return 1
  fi

  # An exact name, compared in the shell: `git ls-remote <url> <pattern>` matches a pattern against
  # the TAIL of a ref, so `some-change` would have found `fix/some-change`, and a `grep -q` that
  # stops at the first match can fail its pipeline under pipefail and read master by mistake.
  local found="" sha ref
  while IFS=$'\t' read -r sha ref; do
    ref=${ref%$'\r'} # a Windows runner's git may end its lines with a carriage return
    if [ -n "$branch" ] && [ "$ref" = "refs/heads/$branch" ]; then found=yes; fi
  done <<< "$heads"

  if [ -n "$found" ]; then
    git clone --quiet --depth 1 --branch="$branch" -- "$url" "$destination" || return 1
    echo "The wiki is read at its branch '$branch', named like this pull request's."
  else
    git clone --quiet --depth 1 -- "$url" "$destination" || return 1
    if [ -n "$branch" ]; then
      echo "The wiki has no branch named '$branch', so it is read at its default branch."
    else
      echo "Not a pull request, so the wiki is read at its default branch."
    fi
  fi
  echo "Checked out: $(git -C "$destination" rev-parse --abbrev-ref HEAD) at $(git -C "$destination" rev-parse --short HEAD)."
}

# Builds a fixture wiki whose default branch and whose pull request branch hold different text in
# one page, and asserts which text each run reads: a run proves what it read by the page, not by an
# exit code. Each run is this script in a process of its own, as CI runs it: called as a function
# inside an `if`, bash would switch `set -e` off in it, and the test would run a different script.
self_test() {
  local self=${BASH_SOURCE[0]}
  local work
  work=$(mktemp -d)
  # shellcheck disable=SC2064 # expanded now, on purpose: $work is local to this function
  trap "rm -rf '$work'" EXIT

  local fixture="$work/wiki"
  git init --quiet "$fixture"
  git -C "$fixture" symbolic-ref HEAD refs/heads/master
  git -C "$fixture" config user.name "Self Test"
  git -C "$fixture" config user.email "self-test@example.invalid"
  git -C "$fixture" config commit.gpgsign false
  printf 'master\n' > "$fixture/Page.md"
  git -C "$fixture" add Page.md
  git -C "$fixture" commit --quiet -m "the default branch"
  git -C "$fixture" checkout --quiet -b fix/some-change
  printf 'branch\n' > "$fixture/Page.md"
  git -C "$fixture" commit --quiet -am "a pull request's pages"
  git -C "$fixture" checkout --quiet master

  local failures=0 runs=0
  expect() {
    local name=$1 branch=$2 want=$3 got
    runs=$((runs + 1))
    rm -rf "$work/out"
    if ! EQ_WIKI_URL="file://$fixture" EQ_WIKI_BRANCH="$branch" bash "$self" "$work/out" > /dev/null 2>&1; then
      echo "FAIL: $name: the checkout failed" >&2
      failures=$((failures + 1))
      return
    fi
    got=$(cat "$work/out/Page.md" 2> /dev/null) || got="(no page)"
    if [ "$got" = "$want" ]; then
      echo "ok: $name"
    else
      echo "FAIL: $name: read '$got', expected '$want'" >&2
      failures=$((failures + 1))
    fi
  }

  expect "a pull request with a wiki branch reads it" "fix/some-change" "branch"
  expect "a pull request without one reads the default branch" "feat/nothing-here" "master"
  expect "a run outside a pull request reads the default branch" "" "master"
  expect "a name that is only the tail of a branch is not that branch" "some-change" "master"
  expect "a name that is not a ref at all reads the default branch" '$(touch pwned) `id` ;x' "master"

  # A wiki that cannot be reached fails, and leaves nothing behind that a guard could read as master.
  runs=$((runs + 1))
  if EQ_WIKI_URL="file://$work/absent" EQ_WIKI_BRANCH="fix/some-change" bash "$self" "$work/none" > /dev/null 2>&1; then
    echo "FAIL: an unreachable wiki was checked out" >&2
    failures=$((failures + 1))
  elif [ -e "$work/none" ]; then
    echo "FAIL: an unreachable wiki left a directory behind" >&2
    failures=$((failures + 1))
  else
    echo "ok: an unreachable wiki fails, and leaves nothing behind"
  fi

  # A wiki whose branches cannot be listed fails even when it could be cloned: reading master then
  # would be the wrong pages for a pull request that carries its own. A `git` that refuses only
  # `ls-remote` stands in for a listing that fails while the clone would work.
  runs=$((runs + 1))
  local real_git
  real_git=$(command -v git)
  mkdir -p "$work/bin"
  printf '#!/usr/bin/env bash\n[ "$1" = ls-remote ] && exit 128\nexec "%s" "$@"\n' "$real_git" > "$work/bin/git"
  chmod +x "$work/bin/git"
  if PATH="$work/bin:$PATH" EQ_WIKI_URL="file://$fixture" EQ_WIKI_BRANCH="fix/some-change" bash "$self" "$work/unlisted" > /dev/null 2>&1; then
    echo "FAIL: a wiki whose branches could not be listed was read at its default branch" >&2
    failures=$((failures + 1))
  elif [ -e "$work/unlisted" ]; then
    echo "FAIL: a wiki whose branches could not be listed left a directory behind" >&2
    failures=$((failures + 1))
  else
    echo "ok: a wiki whose branches cannot be listed fails, even when it could be cloned"
  fi

  # The branch name above was never run: nothing it names exists.
  runs=$((runs + 1))
  if [ -e pwned ] || [ -e "$work/pwned" ]; then
    echo "FAIL: a branch name was executed" >&2
    failures=$((failures + 1))
  else
    echo "ok: a branch name is never executed"
  fi

  if [ "$failures" -ne 0 ]; then
    echo "::error::The wiki checkout's self-test failed $failures of $runs checks." >&2
    return 1
  fi
  echo "The wiki checkout's self-test passed all $runs checks."
}

case "${1:-}" in
  --self-test) self_test ;;
  "" | -*) echo "usage: $0 <destination> | --self-test" >&2; exit 2 ;;
  *) checkout "$1" ;;
esac
