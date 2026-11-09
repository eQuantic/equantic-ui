#!/usr/bin/env bash
# Splits the test projects under tests/ into the groups CI runs as parallel jobs (#1310).
#
# Each leg of the `test` job ran the eleven projects one after another, 13 minutes on Ubuntu and 15 to
# 17 on Windows, and one project dominated: Conformance.Tests, 5 minutes 32 seconds on Ubuntu, then
# Web.Tests at 2 minutes 35. So each system runs three jobs side by side, and the longest one is about
# Conformance alone:
#
#   conformance  the project whose name ends in Conformance.Tests
#   web          the project whose name ends in Web.Tests
#   rest         every other project found under tests/
#
# The projects are FOUND, never listed: `rest` is whatever the first two did not take, so a new
# project lands in a job without an edit to the workflow. A group that finds no project fails rather
# than running nothing, so renaming one of the named projects is a red run, not a silent gap.
#
#   scripts/test-groups.sh --projects <group>   print the group's project files, one a line
#   scripts/test-groups.sh --groups             print the groups' names
#   scripts/test-groups.sh --self-test          prove the split against a fixture tree
#
# EQ_TESTS_ROOT  the tree read, `tests` by default; the self-test points it at its fixture.
set -euo pipefail

readonly GROUPS_IN_ORDER="conformance web rest"

# shellcheck source=scripts/test-projects.sh
. "$(dirname "${BASH_SOURCE[0]}")/test-projects.sh"

# Every test project under the root, sorted, found as the macOS job finds them.
all_projects() { test_projects "${EQ_TESTS_ROOT:-tests}"; }

# The group a project file belongs to, from its name.
group_of() {
  case "$(basename "$1" .csproj)" in
    *Conformance.Tests) echo conformance ;;
    *Web.Tests) echo web ;;
    *) echo rest ;;
  esac
}

projects() {
  local group=$1 project found=""
  case " $GROUPS_IN_ORDER " in
    *" $group "*) ;;
    *) echo "::error::'$group' is not a test group; the groups are: $GROUPS_IN_ORDER." >&2; return 2 ;;
  esac
  local all
  all=$(all_projects) || { echo "::error::The test projects under ${EQ_TESTS_ROOT:-tests} could not be listed." >&2; return 1; }
  while IFS= read -r project; do
    [ -n "$project" ] || continue
    if [ "$(group_of "$project")" = "$group" ]; then
      printf '%s\n' "$project"
      found=yes
    fi
  done <<< "$all"
  if [ -z "$found" ]; then
    echo "::error::The test group '$group' found no project under ${EQ_TESTS_ROOT:-tests}, so its job would run nothing." >&2
    return 1
  fi
}

self_test() {
  local self=${BASH_SOURCE[0]} work
  work=$(mktemp -d)
  # shellcheck disable=SC2064 # expanded now, on purpose: $work is local to this function
  trap "rm -rf '$work'" EXIT
  local p
  for p in X.Conformance.Tests X.Web.Tests X.Compiler.Tests X.Server.Tests X.Brand.New.Tests; do
    mkdir -p "$work/$p"
    touch "$work/$p/$p.csproj"
  done
  mkdir -p "$work/X.Web.Tests/bin/Release"
  touch "$work/X.Web.Tests/bin/Release/Copied.Conformance.Tests.csproj"

  local failures=0 group got want union
  expect() {
    local group=$1 want=$2
    # A group that fails reads as what it printed, nothing, and is reported below: under `set -e` the
    # failing pipeline would otherwise end the self-test with no word said.
    got=$(EQ_TESTS_ROOT="$work" bash "$self" --projects "$group" 2> /dev/null | sed "s#$work/##" | tr '\n' ' ') || true
    if [ "$got" = "$want" ]; then
      echo "ok: $group holds $want"
    else
      echo "FAIL: $group holds '$got', expected '$want'" >&2
      failures=$((failures + 1))
    fi
  }
  expect conformance "X.Conformance.Tests/X.Conformance.Tests.csproj "
  expect web "X.Web.Tests/X.Web.Tests.csproj "
  expect rest "X.Brand.New.Tests/X.Brand.New.Tests.csproj X.Compiler.Tests/X.Compiler.Tests.csproj X.Server.Tests/X.Server.Tests.csproj "

  # Every project is in exactly one group: the groups' union is the tree, with nothing twice.
  union=$(for group in $GROUPS_IN_ORDER; do EQ_TESTS_ROOT="$work" bash "$self" --projects "$group"; done | sort)
  if [ "$union" != "$(EQ_TESTS_ROOT="$work" all_projects)" ] || [ -n "$(uniq -d <<< "$union")" ]; then
    echo "FAIL: the groups do not partition the projects" >&2
    failures=$((failures + 1))
  else
    echo "ok: every project is in exactly one group, and a new one lands in rest"
  fi

  # A named project renamed away leaves its group empty, which fails saying so.
  rm -rf "$work/X.Conformance.Tests"
  local out
  if out=$(EQ_TESTS_ROOT="$work" bash "$self" --projects conformance 2>&1); then
    echo "FAIL: an empty group passed" >&2
    failures=$((failures + 1))
  elif ! /usr/bin/grep -qF "found no project" <<< "$out"; then
    echo "FAIL: an empty group failed for another reason: $out" >&2
    failures=$((failures + 1))
  else
    echo "ok: a group that finds no project fails, saying so"
  fi
  if EQ_TESTS_ROOT="$work" bash "$self" --projects nonsense > /dev/null 2>&1; then
    echo "FAIL: a group that does not exist passed" >&2
    failures=$((failures + 1))
  else
    echo "ok: a group that does not exist fails"
  fi

  if [ "$failures" -ne 0 ]; then
    echo "::error::The test groups' self-test failed $failures checks." >&2
    return 1
  fi
  echo "The test groups' self-test passed."
}

case "${1:-}" in
  --projects) projects "${2:?a group}" ;;
  --groups) echo "$GROUPS_IN_ORDER" ;;
  --self-test) self_test ;;
  *) echo "usage: $0 --projects <group> | --groups | --self-test" >&2; exit 2 ;;
esac
