#!/usr/bin/env bash
# Runs scripts/publish-packages.sh against a stub of the push and of nuget.org's package index, and
# asserts every promise of openspec/specs/release-publishing (#759).
#
# The jobs that publish run only on a `v*` tag or by hand, so without this nothing would run the
# loop until the day a release depends on it, which is the day 0.2.0-preview.61 found the old one
# could not finish. CI's publish-packages job runs this on every pull request and on main.
#
#   ./scripts/check-publish-packages.sh
#
# PUBLISH_BASH  the shell the script under test runs in (`bash`). The tag's pipeline publishes on
#               macos-latest, whose /bin/bash is 3.2: PUBLISH_BASH=/bin/bash on a Mac proves that.
#
# The stub `dotnet` records every push and answers from a plan written per package: one outcome a
# line (ok, 409, 500, 403), the last one repeating. The stub `sleep` records what it was asked to
# wait and returns at once, so the real waits are asserted without being waited. The index is a
# directory of JSON documents read through file://.
#
# Controls keep the check honest: each one breaks the script in one way a release has met or could
# meet, and the case that guards that promise has to fail on it. A control whose edit changed
# nothing fails too, or a renamed line would turn the control into a pass that proves nothing.
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
shell=${PUBLISH_BASH:-bash}
version=9.9.9-check.1
key=check-key-never-printed

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# --- the fixture -------------------------------------------------------------------------------

# A fresh fixture in $fx: empty packages, an empty index, the two stubs and their records.
fixture() {
  fx="$work/$1"
  rm -rf "$fx"
  mkdir -p "$fx/packages" "$fx/index" "$fx/stub" "$fx/plan"
  : > "$fx/calls"; : > "$fx/sleeps"; : > "$fx/arguments"
  cat > "$fx/stub/dotnet" <<'STUB'
#!/usr/bin/env bash
# Stands in for `dotnet nuget push <file> --source <url> --api-key <key> --skip-duplicate`.
here="$(cd "$(dirname "$0")/.." && pwd)"
file=$(basename "$3")
echo "$file" >> "$here/calls"
printf '%s\n' "$*" >> "$here/arguments"
calls=$(grep -cxF "$file" "$here/calls")
outcome=ok
if [ -f "$here/plan/$file" ]; then
  outcome=$(sed -n "${calls}p" "$here/plan/$file")
  [ -n "$outcome" ] || outcome=$(tail -n 1 "$here/plan/$file")
fi
echo "Pushing $file to 'https://www.nuget.org/api/v2/package'..."
echo "  PUT https://www.nuget.org/api/v2/package/"
case "$outcome" in
  ok)
    echo "  Created https://www.nuget.org/api/v2/package/ 578ms"
    echo "Your package was pushed."
    ;;
  409)
    echo "  Conflict https://www.nuget.org/api/v2/package/ 297ms"
    echo "Package '$3' already exists at feed 'https://www.nuget.org/api/v2/package'."
    ;;
  500)
    echo "  InternalServerError https://www.nuget.org/api/v2/package/ 65ms"
    echo "error: Response status code does not indicate success: 500 (Internal Server Error)."
    exit 1
    ;;
  403)
    echo "  Forbidden https://www.nuget.org/api/v2/package/ 120ms"
    echo "error: Response status code does not indicate success: 403 (The specified API key is invalid, has expired, or does not have permission to access the specified package.)."
    exit 1
    ;;
  *) echo "stub: unknown outcome '$outcome'" >&2; exit 99 ;;
esac
STUB
  cat > "$fx/stub/sleep" <<'STUB'
#!/usr/bin/env bash
echo "$1" >> "$(cd "$(dirname "$0")/.." && pwd)/sleeps"
STUB
  chmod +x "$fx/stub/dotnet" "$fx/stub/sleep"
}

# package <id> [listed|elsewhere]: a package of the release, which the index lists at the release's
# version, lists only at another one, or has never seen.
package() {
  : > "$fx/packages/$1.$version.nupkg"
  local lower
  lower=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
  case "${2:-}" in
    listed) mkdir -p "$fx/index/$lower"; printf '{"versions":["0.1.0","%s"]}' "$version" > "$fx/index/$lower/index.json" ;;
    elsewhere) mkdir -p "$fx/index/$lower"; printf '{"versions":["0.1.0","%s0"]}' "$version" > "$fx/index/$lower/index.json" ;;
  esac
}

# plan <id> <outcome...>: what the push of that package answers, call after call.
plan() {
  local id=$1; shift
  printf '%s\n' "$@" > "$fx/plan/$id.$version.nupkg"
}

# publish [package-id ...]: runs the script under test over the fixture. Sets $status, and leaves
# what it printed in $fx/output. The key is the fixture's own ($handed), never one the caller's
# environment happens to export.
handed=$key
publish() {
  PATH="$fx/stub:$PATH" NUGET_API_KEY=$handed PUBLISH_INDEX="file://$fx/index" \
    PUBLISH_WAITS="30 60 120 240" PUBLISH_PAUSE=15 PUBLISH_SOURCE="" \
    "$shell" "$script" "$fx/packages" "$version" "$@" > "$fx/output" 2>&1
  status=$?
}

calls() { tr '\n' ' ' < "$fx/calls" | sed "s/\.$version\.nupkg//g; s/ $//"; }
sleeps() { tr '\n' ' ' < "$fx/sleeps" | sed 's/ $//'; }

# --- the cases: each prints one line per broken promise, and nothing when it holds ---------------

problem() { echo "$case_name: $1"; }
expect() { [ "$2" = "$3" ] || problem "$1: expected '$3', got '$2'"; }

case_listed_is_skipped() {
  case_name="a package nuget.org already lists is not pushed again"
  fixture listed
  package Alpha listed; package Beta; package Gamma listed; package Delta elsewhere; package Epsilon
  publish
  expect "exit" "$status" 0
  expect "pushes" "$(calls)" "Beta Delta Epsilon"
  grep -q 'listed on nuget.org already: 2' "$fx/output" || problem "the summary does not count the two listed"
}

case_failed_push_is_tried_again() {
  case_name="a push that fails for a reason outside the package is tried again"
  fixture again
  package Alpha; package Beta
  plan Alpha 500 500 ok
  publish
  expect "exit" "$status" 0
  expect "pushes" "$(calls)" "Alpha Alpha Alpha Beta"
  # One wait and then a longer one for Alpha, then the pause before Beta.
  expect "waits" "$(sleeps)" "30 60 15"
}

case_goes_on_and_fails_at_the_end() {
  case_name="publishing goes on after a package it could not push and fails at the end"
  fixture goes-on
  package Alpha; package Beta; package Gamma
  plan Beta 500
  publish
  expect "exit" "$status" 1
  expect "pushes" "$(calls)" "Alpha Beta Beta Beta Beta Beta Gamma"
  expect "waits" "$(sleeps)" "15 30 60 120 240 15"
  grep -q 'Not pushed after every try: Beta\.' "$fx/output" || problem "the failure does not name Beta alone"
}

case_refused_push_is_not_tried_again() {
  case_name="a push refused for the key is not tried again, and the list goes on"
  # A key whose scope lacks one package: that one is refused once, and the rest go through.
  fixture key
  package Alpha; package Beta; package Gamma
  plan Beta 403
  publish
  expect "exit" "$status" 1
  expect "pushes" "$(calls)" "Alpha Beta Gamma"
  expect "waits" "$(sleeps)" "15 15"
  grep -q 'refused the key (401 or 403), and these were not tried again: Beta\.' "$fx/output" || problem "the failure does not name Beta as refused"
  # An expired key: every package is refused once, with no wait spent on any.
  fixture expired
  package Alpha; package Beta
  plan Alpha 403; plan Beta 403
  publish
  expect "exit with an expired key" "$status" 1
  expect "pushes with an expired key" "$(calls)" "Alpha Beta"
  expect "waits with an expired key" "$(sleeps)" "15"
}

case_stray_version_is_refused() {
  case_name="a package at another version is refused, not left out"
  fixture stray
  package Alpha; package Beta
  : > "$fx/packages/Gamma.1.0.0.nupkg"
  publish
  expect "exit" "$status" 1
  expect "pushes" "$(calls)" ""
  grep -q 'Gamma.1.0.0.nupkg' "$fx/output" || problem "the failure does not name the stray package"
}

case_index_version_is_compared_as_the_index_writes_it() {
  case_name="a version is found listed as the index writes it"
  # The index lower-cases a version and drops its build metadata.
  fixture written
  local release=$version
  version=9.9.9-check.1
  package Alpha listed
  mv "$fx/packages/Alpha.$version.nupkg" "$fx/packages/Alpha.9.9.9-CHECK.1+build.7.nupkg"
  version=9.9.9-CHECK.1+build.7
  publish
  version=$release
  expect "exit" "$status" 0
  expect "pushes" "$(calls)" ""
}

case_given_ids_only() {
  case_name="the packages it was given, and no other"
  fixture given
  package Alpha; package Beta; package Gamma
  publish Gamma Alpha
  expect "exit" "$status" 0
  expect "pushes" "$(calls)" "Gamma Alpha"
}

case_unknown_id_is_refused_first() {
  case_name="a package id the run did not build is refused before anything is pushed"
  fixture unknown
  package Alpha; package Beta
  publish Alpha Missing
  expect "exit" "$status" 1
  expect "pushes" "$(calls)" ""
  grep -q 'Missing' "$fx/output" || problem "the failure does not name the id"
  publish '../Alpha'
  expect "exit for a path" "$status" 2
  expect "pushes after a path" "$(calls)" ""
}

case_duplicate_counts_as_pushed() {
  case_name="a package pushed a moment ago and not listed yet counts as pushed"
  fixture duplicate
  package Alpha; package Beta
  plan Alpha 409
  publish
  expect "exit" "$status" 0
  expect "pushes" "$(calls)" "Alpha Beta"
}

case_key_is_handed_over_and_never_printed() {
  case_name="the key reaches the client and no log"
  fixture printed
  package Alpha
  plan Alpha 500 ok
  publish
  expect "exit" "$status" 0
  grep -q -- "--api-key $key --skip-duplicate" "$fx/arguments" || problem "the client was not handed the key with --skip-duplicate"
  if grep -qF "$key" "$fx/output"; then problem "the key is in the output"; fi
}

case_nothing_to_publish_fails() {
  case_name="a run with no package at the version, or no key, pushes nothing and fails"
  fixture nothing
  publish
  expect "exit with no package" "$status" 1
  package Alpha
  handed=""
  publish
  handed=$key
  expect "exit with no key" "$status" 1
  expect "pushes" "$(calls)" ""
}

run_cases() {
  script=$1
  case_listed_is_skipped
  case_failed_push_is_tried_again
  case_goes_on_and_fails_at_the_end
  case_refused_push_is_not_tried_again
  case_stray_version_is_refused
  case_index_version_is_compared_as_the_index_writes_it
  case_given_ids_only
  case_unknown_id_is_refused_first
  case_duplicate_counts_as_pushed
  case_key_is_handed_over_and_never_printed
  case_nothing_to_publish_fails
}

# --- the script as it is ---------------------------------------------------------------------------

failures=0
real="$root/scripts/publish-packages.sh"
problems=$(run_cases "$real")
if [ -n "$problems" ]; then
  echo "$problems" | sed 's/^/FAIL: /' >&2
  failures=$((failures + $(echo "$problems" | wc -l)))
else
  echo "ok: every promise holds for scripts/publish-packages.sh under $("$shell" --version | head -n 1)"
fi

# --- the controls ----------------------------------------------------------------------------------

# control <what it breaks> <the case that must fail> <sed expression>
control() {
  local broken="$work/control.sh"
  sed -e "$3" "$real" > "$broken"
  if cmp -s "$real" "$broken"; then
    echo "FAIL: the control '$1' changed nothing: its edit no longer matches the script" >&2
    failures=$((failures + 1))
    return
  fi
  # Collected whole, then matched in the shell: under pipefail a `grep -q` that stops at its first
  # match fails the pipeline, and every control would read as a pass that should have failed.
  local found
  found=$(run_cases "$broken")
  case $'\n'"$found" in
    *$'\n'"$2:"*) echo "ok: a script that $1 fails '$2'" ;;
    *)
      echo "FAIL: a script that $1 passes '$2'" >&2
      failures=$((failures + 1))
      ;;
  esac
}

control "stops at the first package it cannot push" \
  "publishing goes on after a package it could not push and fails at the end" \
  's/^    \*) owed="\$owed \$id" ;;$/    *) echo "stopped on $id"; exit 1 ;;/'
control "pushes what nuget.org already lists" \
  "a package nuget.org already lists is not pushed again" \
  's/^listed() {$/listed() { return 1/'
control "tries a refused key again" \
  "a push refused for the key is not tried again, and the list goes on" \
  's/^    return 2$/    return 1/'
control "stops at a refused key" \
  "a push refused for the key is not tried again, and the list goes on" \
  's/^    2) refused="\$refused \$id" ;;$/    2) echo "refused on $id"; exit 1 ;;/'
control "leaves out a package at another version" \
  "a package at another version is refused, not left out" \
  's/^      \*) strays="\$strays \${file##\*\/}" ;;$/      *) : ;;/'
control "compares the version as it was typed" \
  "a version is found listed as the index writes it" \
  's/^listed_version=.*$/listed_version=$version/'
control "tries again without waiting" \
  "a push that fails for a reason outside the package is tried again" \
  's/^      sleep "\$delay"$/      :/'
control "prints the key" \
  "the key reaches the client and no log" \
  's/^  \[ "\$status" -eq 0 \] && return 0$/  echo "key $NUGET_API_KEY"; [ "$status" -eq 0 ] \&\& return 0/'

if [ "$failures" -gt 0 ]; then
  echo "check-publish-packages.sh: $failures failure(s)" >&2
  exit 1
fi
echo "check-publish-packages.sh: the script holds, and each control fails the case that guards it"
