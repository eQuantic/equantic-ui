#!/usr/bin/env bash
# Runs the two files a change writes for itself and a release folds (#1489) through what writes and
# folds them, in repositories of their own, and asserts what a change and a release ask of each:
#
#   scripts/public-api.sh          `update` against a stub `dotnet`, and `ship`
#   scripts/fold-runtime-budget.sh the served runtime's deltas folded into its base
#
# `update` rebuilds every project, and both folds run only at a release, so none of it ran anywhere a
# pull request could see before this did. CI's publish-packages job runs it on every pull request and
# on main, beside the check of the loop that publishes.
#
#   ./scripts/check-release-folds.sh
#
# The stub prints the diagnostics a build of the one project would, RS0016 against a source file and
# RS0017 against the file that declares the entry, and fails as the build does. It answers for the
# first round's two builds and is quiet after, which is a build whose surface settled.
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

fail() {
  echo "FAIL: $*" >&2
  failures=$((failures + 1))
}

# A repository with one shipping project, on BRANCH, and the stub on PATH.
fixture() {
  local branch="$1"
  repo="$work/$2"
  rm -rf "$repo"
  mkdir -p "$repo/scripts" "$repo/src/eQuantic.UI.Probe" "$repo/stub"
  cp "$root/scripts/public-api.sh" "$repo/scripts/"
  api="$repo/src/eQuantic.UI.Probe"
  printf '<Project Sdk="Microsoft.NET.Sdk"></Project>\n' > "$api/eQuantic.UI.Probe.csproj"
  printf '#nullable enable\nProbe.Shipped -> int\nProbe.Kept -> int\n' > "$api/PublicAPI.Shipped.txt"
  printf '#nullable enable\nProbe.Root -> int\n' > "$api/PublicAPI.Unshipped.txt"
  : > "$repo/stub/calls"
  : > "$repo/stub/diagnostics"
  cat > "$repo/stub/dotnet" <<EOF
#!/usr/bin/env bash
calls=\$(wc -l < "$repo/stub/calls")
echo build >> "$repo/stub/calls"
[ "\$calls" -lt 2 ] || exit 0
sed "s|@API@|$api|g" "$repo/stub/diagnostics"
exit 1
EOF
  chmod +x "$repo/stub/dotnet"
  git -C "$repo" init -q
  git -C "$repo" checkout -q -b "$branch"
  git -C "$repo" add -A
  git -C "$repo" -c user.email=check@example.invalid -c user.name=check commit -q -m fixture
}

# A diagnostic the stub prints: RS0016 for ENTRY, or RS0017 for ENTRY declared in FILE.
added() { echo "@API@/Probe.cs(1,1): error RS0016: Symbol '$1' is not part of the declared public API (https://example.invalid)" >> "$repo/stub/diagnostics"; }
gone() { echo "@API@/$2(2,1): error RS0017: Symbol '$1' is part of the declared API, but is either not public or could not be found (https://example.invalid)" >> "$repo/stub/diagnostics"; }

update() { (cd "$repo" && PATH="$repo/stub:$PATH" "$@" ./scripts/public-api.sh update) > "$repo/out" 2>&1; }

lines() { grep -v '^#nullable enable$' "$1" 2>/dev/null | sort; }

# --- a change writes its own file ---------------------------------------------------------------

fixture feat/a-probe-grows grows
added 'Probe.Added -> int'
gone 'Probe.Shipped -> int' PublicAPI.Shipped.txt
gone 'Probe.Root -> int' PublicAPI.Unshipped.txt
update env || fail "update on a branch exited non-zero: $(tail -3 "$repo/out")"
own="$api/PublicAPI.Unshipped.feat-a-probe-grows.txt"
[ -f "$own" ] || fail "update wrote no PublicAPI.Unshipped.feat-a-probe-grows.txt, the file the branch names"
[ "$(lines "$own")" = "$(printf '%s\n' '*REMOVED*Probe.Shipped -> int' 'Probe.Added -> int' | sort)" ] \
  || fail "the change's file holds $(lines "$own" | tr '\n' '|'), not the addition and the retired shipped entry"
[ -z "$(lines "$api/PublicAPI.Unshipped.txt")" ] \
  || fail "the project's own Unshipped holds $(lines "$api/PublicAPI.Unshipped.txt" | tr '\n' '|'): nothing is written there, and the entry RS0017 named goes"

# --- an entry another change declared is declared once ------------------------------------------

fixture fix/a-probe-repeats repeats
printf '#nullable enable\nProbe.Added -> int\n' > "$api/PublicAPI.Unshipped.feat-other.txt"
printf '#nullable enable\nProbe.Leaving -> int\n' > "$api/PublicAPI.Unshipped.feat-leaving.txt"
added 'Probe.Added -> int'
gone 'Probe.Leaving -> int' PublicAPI.Unshipped.feat-leaving.txt
update env || fail "update exited non-zero: $(tail -3 "$repo/out")"
[ ! -f "$api/PublicAPI.Unshipped.fix-a-probe-repeats.txt" ] \
  || fail "an entry another change's file declares was declared again, which is RS0025"
[ -z "$(lines "$api/PublicAPI.Unshipped.feat-other.txt")" ] && fail "another change's entry was taken out of its file"
[ ! -f "$api/PublicAPI.Unshipped.feat-leaving.txt" ] \
  || fail "the entry RS0017 named in a change's file stayed, or the file it emptied did"

# --- no branch names no file --------------------------------------------------------------------

fixture main on-main
added 'Probe.Added -> int'
update env && fail "update on main wrote somewhere instead of refusing"
grep -q 'EQ_API_CHANGE' "$repo/out" || fail "the refusal on main does not name EQ_API_CHANGE, the way on"
update env EQ_API_CHANGE=chore/named-by-hand || fail "update with EQ_API_CHANGE exited non-zero: $(tail -3 "$repo/out")"
[ -f "$api/PublicAPI.Unshipped.chore-named-by-hand.txt" ] || fail "EQ_API_CHANGE did not name the change's file"

# --- a release folds every file -----------------------------------------------------------------

fixture main ships
printf '#nullable enable\nProbe.Added -> int\n*REMOVED*Probe.Shipped -> int\n' > "$api/PublicAPI.Unshipped.feat-one.txt"
printf '#nullable enable\nProbe.Other -> int\n' > "$api/PublicAPI.Unshipped.fix-two.txt"
(cd "$repo" && ./scripts/public-api.sh ship) > "$repo/out" 2>&1 || fail "ship exited non-zero: $(tail -3 "$repo/out")"
[ "$(lines "$api/PublicAPI.Shipped.txt")" = "$(printf '%s\n' 'Probe.Added -> int' 'Probe.Kept -> int' 'Probe.Other -> int' 'Probe.Root -> int' | sort)" ] \
  || fail "Shipped holds $(lines "$api/PublicAPI.Shipped.txt" | tr '\n' '|') after the fold, not every file's entries less the retired one"
[ -z "$(lines "$api/PublicAPI.Unshipped.txt")" ] || fail "the project's own Unshipped was not emptied by the fold"
ls "$api"/PublicAPI.Unshipped.*.txt >/dev/null 2>&1 && fail "a change's file is still there after the fold"

# --- the runtime budget's deltas fold into its base ---------------------------------------------

budget_fixture() {
  repo="$work/$1"
  rm -rf "$repo"
  budgets="$repo/tests/eQuantic.UI.Server.Tests/Budgets"
  mkdir -p "$budgets/served-runtime"
  printf '{\n  "gzipBytes": 1000,\n  "measuredWith": "gzip, CompressionLevel.SmallestSize"\n}\n' > "$budgets/served-runtime.json"
  printf '{ "deltaBytes": 30, "bought": "a component" }\n' > "$budgets/served-runtime/feat-a-grows.json"
  printf '{ "deltaBytes": -10, "bought": "a smaller lowering" }\n' > "$budgets/served-runtime/fix-b-shrinks.json"
}

budget_fixture folds
"$root/scripts/fold-runtime-budget.sh" "$repo" > "$repo/out" 2>&1 || fail "the fold exited non-zero: $(tail -3 "$repo/out")"
grep -q '"gzipBytes": 1020,' "$budgets/served-runtime.json" \
  || fail "the base is $(grep gzipBytes "$budgets/served-runtime.json"), not 1000 plus both deltas"
grep -q '"measuredWith": "gzip, CompressionLevel.SmallestSize"' "$budgets/served-runtime.json" \
  || fail "the fold lost how the base is measured"
ls "$budgets"/served-runtime/*.json >/dev/null 2>&1 && fail "a delta is still there after the fold"
grep -q 'a component' "$repo/out" || fail "the fold did not print what each delta bought"

budget_fixture refuses
printf '{ "deltaBytes": 5 }\n' > "$budgets/served-runtime/feat-c-says-nothing.json"
"$root/scripts/fold-runtime-budget.sh" "$repo" > "$repo/out" 2>&1 && fail "the fold took a delta that says nothing it bought"
grep -q '"gzipBytes": 1000,' "$budgets/served-runtime.json" || fail "a refused fold wrote the base anyway"
[ -f "$budgets/served-runtime/feat-a-grows.json" ] || fail "a refused fold removed a delta"

if [ "$failures" -gt 0 ]; then
  echo "$failures check(s) failed." >&2
  exit 1
fi
echo "The declared API and the runtime budget are written per change and folded whole: every case holds."
