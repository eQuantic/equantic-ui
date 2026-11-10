#!/usr/bin/env bash
# Runs, on macOS, the test projects that hold what only a Mac answers (#1311), and only those.
#
# The full suite runs on Linux and Windows, every project. It also ran a third time on macOS inside
# `build-packages`, 13 minutes 38 seconds on the run's longest path, for the facts that only a Mac
# answers: CoreText, CoreGraphics, Metal, AppKit, the Objective-C runtime and the Apple workloads,
# which Linux and Windows skip. Most projects answer the same on every system, so this runs the others.
#
# WHICH projects is DERIVED, never listed, and a project is the unit: a test that asks for a Mac does
# it in many ways (a MacFact, `OperatingSystem.IsMacOS()`, a Metal device it probes, a workload it
# looks for, a helper in another file, a nested class), and only the project is sure to hold all of
# them. A project runs here when
#
#   - it references a project of the Apple track (`Native.Shell.Apple`, `.MacOS`, `.iOS`,
#     `Native.Engine.Metal`), or
#   - one of its files holds a test attribute and asks for macOS: a MacFact, MacTheory or MacFontFact,
#     `OperatingSystem.IsMacOS()`, `OSPlatform.OSX` or `[SupportedOSPlatform("macos")]`.
#
# A project that runs no test fails, and so does a derivation that finds no project.
#
#   scripts/macos-tests.sh --list        print "<project file><TAB><why>" for each project to run
#   scripts/macos-tests.sh --run         run each one whole, and fail when one runs no test
#   scripts/macos-tests.sh --count <trx> the number of tests a TRX report says ran
#   scripts/macos-tests.sh --self-test   prove the derivation and the count against fixtures
#
# EQ_TESTS_ROOT  the tree the derivation reads, `tests` by default; the self-test points it at its fixture.
set -euo pipefail

# An attribute is followed by `]`, by its arguments or by another attribute: `[MacFact]`,
# `[MacFontFact("Menlo")]`, `[Fact, Trait(...)]`. `[UiFactory]` is none of them.
readonly MARKERS='OperatingSystem\.IsMacOS\(\)|OSPlatform\.OSX|\[Mac(Fact|Theory|FontFact)[](,]|SupportedOSPlatform\("macos"\)'
readonly TEST_ATTRIBUTE='\[[A-Za-z]*(Fact|Theory)[](,]'
readonly APPLE_TRACK='Native\.(Shell\.(Apple|MacOS|iOS)|Engine\.Metal)'

# shellcheck source=scripts/test-projects.sh
. "$(dirname "${BASH_SOURCE[0]}")/test-projects.sh"

list() {
  local root=${EQ_TESTS_ROOT:-tests} project dir file why all
  all=$(test_projects "$root") || { echo "::error::The test projects under $root could not be listed." >&2; return 1; }
  while IFS= read -r project; do
    [ -n "$project" ] || continue
    dir=$(dirname "$project")
    why=""
    if /usr/bin/grep -qE "<ProjectReference[^>]*$APPLE_TRACK" "$project"; then
      why="references the Apple track"
    else
      while IFS= read -r file; do
        [ -n "$file" ] || continue
        if /usr/bin/grep -qE "$TEST_ATTRIBUTE" "$file"; then
          why="$(basename "$file" .cs) asks for macOS"
          break
        fi
      done < <(/usr/bin/grep -rlE --include='*.cs' --exclude-dir=bin --exclude-dir=obj "$MARKERS" "$dir" | sort)
    fi
    if [ -n "$why" ]; then printf '%s\t%s\n' "$project" "$why"; fi
  done <<< "$all"
}

# The number of tests a TRX report says ran, passed and failed included, skipped not.
count() {
  python3 - "$1" <<'PY'
import sys, xml.etree.ElementTree as ET
counters = ET.parse(sys.argv[1]).getroot().find(".//{*}ResultSummary/{*}Counters")
print(int(counters.get("executed", "0")) if counters is not None else 0)
PY
}

run() {
  local entries project why name results status=0 ran=0 failed="" executed
  entries=$(list)
  # A derivation that finds nothing is a broken derivation, not a suite with no Apple facts: the
  # native suites have hundreds of them.
  if [ -z "$entries" ]; then
    echo "::error::No test project under ${EQ_TESTS_ROOT:-tests} asks for macOS, so the derivation found nothing to run."
    return 1
  fi
  results=$(mktemp -d)
  while IFS=$'\t' read -r project why; do
    name=$(basename "$project" .csproj)
    ran=$((ran + 1))
    echo "::group::$name ($why)"
    if ! dotnet test "$project" --configuration Release \
      --logger "trx;LogFileName=$name.trx" --logger "console;verbosity=normal" --results-directory "$results" \
      --blame-hang-timeout 15m --blame-hang-dump-type none; then
      status=1
      failed="$failed$name"$'\n'
    elif [ ! -f "$results/$name.trx" ] || [ "$(count "$results/$name.trx")" -eq 0 ]; then
      # Green with nothing run is the silence this exists to break.
      echo "::error::$name ran no test, so it proved nothing."
      status=1
      failed="$failed$name (ran nothing)"$'\n'
    else
      executed=$(count "$results/$name.trx")
      echo "$name: $executed tests ran on macOS."
    fi
    echo "::endgroup::"
  done <<< "$entries"
  echo "Ran $ran test projects on macOS."
  if [ "$status" -ne 0 ]; then
    echo "::error::These projects failed on macOS:"
    printf '%s' "$failed"
  fi
  return $status
}

# Fixtures for every shape the derivation and the count have to tell apart. Each verdict is read
# from what the script printed, not from its exit code alone.
self_test() {
  local self=${BASH_SOURCE[0]} work
  work=$(mktemp -d)
  # shellcheck disable=SC2064 # expanded now, on purpose: $work is local to this function
  trap "rm -rf '$work'" EXIT
  local p
  for p in A B C D E; do mkdir -p "$work/$p.Tests"; printf '<Project></Project>\n' > "$work/$p.Tests/$p.Tests.csproj"; done
  mkdir -p "$work/A.Tests/Nested" "$work/D.Tests/bin"
  # A: a nested file whose test asks for macOS through its type's platform.
  printf '[SupportedOSPlatform("macos")] class WheelTests { [SkippableTheory] public void Rolls(int n) {} }\n' > "$work/A.Tests/Nested/WheelTests.cs"
  printf 'class PlainTests { [Fact] public void Adds() {} }\n' > "$work/A.Tests/PlainTests.cs"
  # B: references the Apple track and asks nothing in its files (its Metal facts probe the device).
  printf '<Project><ItemGroup><ProjectReference Include="..\\..\\src\\X.Native.Engine.Metal\\X.Native.Engine.Metal.csproj" /></ItemGroup></Project>\n' > "$work/B.Tests/B.Tests.csproj"
  printf 'class ParityTests { [SkippableFact] public void Draws() { Skip.IfNot(Metal.IsSupported); } }\n' > "$work/B.Tests/ParityTests.cs"
  # C: an attribute followed by another one.
  printf 'class TraitTests { [MacFact, Trait("x", "y")] public void Tags() {} }\n' > "$work/C.Tests/TraitTests.cs"
  # D: asks for macOS only in a file with no test, a factory that is not a test, and a build output.
  printf 'class Executor { string Bun => OperatingSystem.IsMacOS() ? "darwin" : "linux"; }\n' > "$work/D.Tests/Executor.cs"
  printf 'class FactoryTests { [UiFactory] public void Builds() { if (OperatingSystem.IsMacOS()) {} } }\n' > "$work/D.Tests/FactoryTests.cs"
  printf 'class BuiltTests { [MacFact] public void Old() {} }\n' > "$work/D.Tests/bin/BuiltTests.cs"
  # E: another system's fact, and a test that asks nothing of the system.
  printf 'class WindowsTests { [WindowsFact] public void Reads() {} }\n' > "$work/E.Tests/WindowsTests.cs"

  local want got
  want=$(printf '%s\t%s\n%s\t%s\n%s\t%s' \
    "$work/A.Tests/A.Tests.csproj" "WheelTests asks for macOS" \
    "$work/B.Tests/B.Tests.csproj" "references the Apple track" \
    "$work/C.Tests/C.Tests.csproj" "TraitTests asks for macOS")
  got=$(EQ_TESTS_ROOT="$work" bash "$self" --list)
  if [ "$got" != "$want" ]; then
    echo "FAIL: the derivation listed:" >&2
    printf '%s\n' "$got" >&2
    echo "expected:" >&2
    printf '%s\n' "$want" >&2
    return 1
  fi
  echo "ok: a project whose test asks for macOS, in any of its files, is listed"
  echo "ok: a project that references the Apple track is listed, whatever its files say"
  echo "ok: an attribute followed by another one is read"
  echo "ok: a file with no test, a factory, a build output and another system's fact list nothing"

  # An empty derivation fails --run, saying why, rather than passing with nothing run.
  local out
  if out=$(EQ_TESTS_ROOT="$work/E.Tests" bash "$self" --run 2>&1); then
    echo "FAIL: --run passed a tree in which nothing asks for macOS" >&2
    return 1
  elif ! /usr/bin/grep -qF "asks for macOS, so the derivation found nothing to run" <<< "$out"; then
    echo "FAIL: --run failed on an empty derivation for another reason:" >&2
    printf '%s\n' "$out" >&2
    return 1
  fi
  echo "ok: a derivation that finds nothing fails --run, saying so"

  # The count a project's guard reads: a report that ran nothing is zero, one that ran tests is their number.
  local trx='<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary outcome="Completed"><Counters total="%s" executed="%s" passed="%s" failed="0" /></ResultSummary></TestRun>\n'
  printf "$trx" 4 0 0 > "$work/none.trx"
  printf "$trx" 9 7 7 > "$work/some.trx"
  if [ "$(bash "$self" --count "$work/none.trx")" != 0 ] || [ "$(bash "$self" --count "$work/some.trx")" != 7 ]; then
    echo "FAIL: the count read $(bash "$self" --count "$work/none.trx") and $(bash "$self" --count "$work/some.trx"), expected 0 and 7" >&2
    return 1
  fi
  echo "ok: a report that ran nothing counts zero, and one that ran seven counts seven"
  echo "The macOS tests' self-test passed."
}

case "${1:-}" in
  --list) list ;;
  --run) run ;;
  --count) count "${2:?a TRX report}" ;;
  --self-test) self_test ;;
  *) echo "usage: $0 --list | --run | --count <trx> | --self-test" >&2; exit 2 ;;
esac
