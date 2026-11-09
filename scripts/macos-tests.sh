#!/usr/bin/env bash
# Runs, on macOS, the tests that need a Mac (#1311), and only those.
#
# The full suite runs on Linux and Windows, every project. It also ran a third time on macOS inside
# `build-packages`, 13 minutes 38 seconds on the run's longest path, for the facts that only a Mac
# answers: CoreText, CoreGraphics, Metal, AppKit and the Objective-C runtime, which Linux and Windows
# skip. Those facts are a small part of the suite, so this runs them alone.
#
# WHICH tests is DERIVED, never listed. A test class asks for macOS in its own file: a MacFact,
# MacTheory or MacFontFact, `OperatingSystem.IsMacOS()`, `OSPlatform.OSX`, or
# `[SupportedOSPlatform("macos")]`. A .cs file declares one type and takes its name (CLAUDE.md, "One
# type per file"), so a file that holds one of those markers and a test attribute names the class to
# run. A new Apple-only fact is picked up by the attribute it has to carry, and a project with none
# is not built here at all.
#
#   scripts/macos-tests.sh --list        print "<project file><TAB><filter>" for each project with such a test
#   scripts/macos-tests.sh --run         run each project with its filter, and fail when one runs no test
#   scripts/macos-tests.sh --self-test   prove the derivation against a fixture tree
#
# EQ_TESTS_ROOT  the tree the derivation reads, `tests` by default; the self-test points it at its fixture.
set -euo pipefail

# An attribute is followed by `]`, by its arguments or by another attribute: `[MacFact]`,
# `[MacFontFact("Menlo")]`, `[Fact, Trait(...)]`. `[UiFactory]` is none of them.
readonly MARKERS='OperatingSystem\.IsMacOS\(\)|OSPlatform\.OSX|\[Mac(Fact|Theory|FontFact)[](,]|SupportedOSPlatform\("macos"\)'
readonly TEST_ATTRIBUTE='\[[A-Za-z]*(Fact|Theory)[](,]'

# Each project under the root that holds a test class asking for macOS, with the filter that selects
# those classes: `FullyQualifiedName~.<Class>.`, the dots on both sides so `MacKeysTests` never selects
# `MacKeysTestsMore`, and a nested class (`Outer+Inner`) is still selected through its outer one.
list() {
  local root=${EQ_TESTS_ROOT:-tests} project dir file class filter
  while IFS= read -r project; do
    dir=$(dirname "$project")
    filter=""
    while IFS= read -r file; do
      [ -n "$file" ] || continue
      /usr/bin/grep -qE "$TEST_ATTRIBUTE" "$file" || continue
      class=$(basename "$file" .cs)
      filter="${filter:+$filter|}FullyQualifiedName~.$class."
    done < <(/usr/bin/grep -rlE --include='*.cs' --exclude-dir=bin --exclude-dir=obj "$MARKERS" "$dir" | sort)
    if [ -n "$filter" ]; then printf '%s\t%s\n' "$project" "$filter"; fi
  done < <(find "$root" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | sort)
}

# The number of tests a TRX report says ran, passed and failed included, skipped not.
executed() {
  python3 - "$1" <<'PY'
import sys, xml.etree.ElementTree as ET
counters = ET.parse(sys.argv[1]).getroot().find(".//{*}ResultSummary/{*}Counters")
print(int(counters.get("executed", "0")) if counters is not None else 0)
PY
}

run() {
  local entries project filter name results status=0 ran=0 failed="" count
  entries=$(list)
  # A derivation that finds nothing is a broken derivation, not a suite with no Apple facts: the
  # native suites have dozens of them.
  if [ -z "$entries" ]; then
    echo "::error::No test class under ${EQ_TESTS_ROOT:-tests} asks for macOS, so the derivation found nothing to run."
    return 1
  fi
  results=$(mktemp -d)
  while IFS=$'\t' read -r project filter; do
    name=$(basename "$project" .csproj)
    ran=$((ran + 1))
    echo "::group::$name ($(tr '|' '\n' <<< "$filter" | wc -l | tr -d ' ') classes)"
    echo "$filter" | tr '|' '\n'
    if ! dotnet test "$project" --configuration Release --filter "$filter" \
      --logger "trx;LogFileName=$name.trx" --results-directory "$results" \
      --blame-hang-timeout 15m --blame-hang-dump-type none; then
      status=1
      failed="$failed$name"$'\n'
    elif [ ! -f "$results/$name.trx" ] || [ "$(executed "$results/$name.trx")" -eq 0 ]; then
      # Green with nothing run is the silence this exists to break: a renamed class, a filter that
      # stopped matching.
      echo "::error::$name ran no test with its filter, so it proved nothing."
      status=1
      failed="$failed$name (ran nothing)"$'\n'
    else
      count=$(executed "$results/$name.trx")
      echo "$name: $count tests ran on macOS."
    fi
    echo "::endgroup::"
  done <<< "$entries"
  echo "Ran the macOS tests of $ran projects."
  if [ "$status" -ne 0 ]; then
    echo "::error::These projects failed on macOS:"
    printf '%s' "$failed"
  fi
  return $status
}

# A fixture tree with every shape the derivation has to tell apart, and the list it must print.
self_test() {
  local work
  work=$(mktemp -d)
  # shellcheck disable=SC2064 # expanded now, on purpose: $work is local to this function
  trap "rm -rf '$work'" EXIT
  mkdir -p "$work/A.Tests/bin" "$work/B.Tests" "$work/C.Tests"
  touch "$work/A.Tests/A.Tests.csproj" "$work/B.Tests/B.Tests.csproj" "$work/C.Tests/C.Tests.csproj"
  printf 'class GlyphTests { [MacFact] public void Draws() {} }\n' > "$work/A.Tests/GlyphTests.cs"
  printf 'class KeysTests { [Fact] public void Maps() { if (OperatingSystem.IsMacOS()) {} } }\n' > "$work/A.Tests/KeysTests.cs"
  printf '[SupportedOSPlatform("macos")] class WheelTests { [SkippableTheory] public void Rolls(int n) {} }\n' > "$work/A.Tests/WheelTests.cs"
  printf 'class MacFactAttribute : FactAttribute { bool M => OperatingSystem.IsMacOS(); }\n' > "$work/A.Tests/MacFactAttribute.cs"
  printf 'class PlainTests { [Fact] public void Adds() {} }\n' > "$work/A.Tests/PlainTests.cs"
  printf 'class WindowsTests { [WindowsFact] public void Reads() {} }\n' > "$work/A.Tests/WindowsTests.cs"
  printf 'class BuiltTests { [MacFact] public void Old() {} }\n' > "$work/A.Tests/bin/BuiltTests.cs"
  printf 'class ExecutorTests { [Theory] public void Runs() { var x = OSPlatform.OSX; } }\n' > "$work/B.Tests/ExecutorTests.cs"
  printf 'class JustTests { [Fact] public void Is() {} }\n' > "$work/C.Tests/JustTests.cs"
  printf 'class TraitTests { [MacFact, Trait("x", "y")] public void Tags() {} }\n' > "$work/B.Tests/TraitTests.cs"
  printf 'class FactoryTests { [UiFactory] public void Builds() { if (OperatingSystem.IsMacOS()) {} } }\n' > "$work/C.Tests/FactoryTests.cs"

  local want got
  want=$(printf '%s\t%s\n%s\t%s' \
    "$work/A.Tests/A.Tests.csproj" "FullyQualifiedName~.GlyphTests.|FullyQualifiedName~.KeysTests.|FullyQualifiedName~.WheelTests." \
    "$work/B.Tests/B.Tests.csproj" "FullyQualifiedName~.ExecutorTests.|FullyQualifiedName~.TraitTests.")
  got=$(EQ_TESTS_ROOT="$work" list)
  if [ "$got" != "$want" ]; then
    echo "FAIL: the derivation listed:" >&2
    printf '%s\n' "$got" >&2
    echo "expected:" >&2
    printf '%s\n' "$want" >&2
    return 1
  fi
  echo "ok: a class that asks for macOS by attribute, by a call or by the type's platform is listed"
  echo "ok: an attribute followed by another one is read, and a factory that is not a test is not"
  echo "ok: a file with no test, a plain test, another system's fact and a build output are not"
  echo "ok: a project with no such class is not listed"

  # An empty derivation fails --run rather than passing with nothing run.
  if EQ_TESTS_ROOT="$work/C.Tests" bash "${BASH_SOURCE[0]}" --run > /dev/null 2>&1; then
    echo "FAIL: --run passed a tree in which nothing asks for macOS" >&2
    return 1
  fi
  echo "ok: a derivation that finds nothing fails --run"
  echo "The macOS tests' self-test passed."
}

case "${1:-}" in
  --list) list ;;
  --run) run ;;
  --self-test) self_test ;;
  *) echo "usage: $0 --list | --run | --self-test" >&2; exit 2 ;;
esac
