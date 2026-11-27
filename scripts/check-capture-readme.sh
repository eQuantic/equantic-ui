#!/usr/bin/env bash
# Runs the phone targets of scripts/capture-readme.sh against stubs of the programs they start, and
# asserts that a picture which cannot be taken stops the target at once, saying why (#1198):
#
#   a python3 with no Pillow      said before anything is built
#   a screenshot that fails       the simulator's or the emulator's own words, after one try
#   a check that refuses          its own reason, after one try
#   a picture of nothing          waited out, the one refusal another picture cures
#   the simulator's screenshot    written where the simulator may write, then moved into the raw folder
#
# Both causes were found on a Mac with the repository on an external volume, where the simulator could
# not write under /Volumes and the python3 had no Pillow. Each was sent to /dev/null, so the target ended
# after a minute of retries with "no capture there", or with no line at all. A real simulator is slow and
# none runs in CI, so the stubs stand in for xcrun, python3, dotnet, uname and sleep, and each case reads
# what a stub wrote to show it ran. CI's publish-packages job runs it beside the other script checks.
#
#   ./scripts/check-capture-readme.sh
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

failed() {
  echo "FAIL: $*" >&2
  failures=$((failures + 1))
}

# A folder of stubs for CASE, first on PATH, and a raw folder of its own. Each stub appends a line to
# calls/<its name> when it runs, so a case counts what ran.
fixture() {
  case_dir="$work/$1"
  stubs="$case_dir/stub"
  raw="$case_dir/raw"
  mkdir -p "$stubs" "$raw" "$case_dir/calls" "$case_dir/tmp"
  # The answers the stub check gives, one a line, read in turn; the last one holds after.
  : > "$case_dir/checks"

  cat > "$stubs/python3" <<EOF
#!/usr/bin/env bash
echo "\$*" >> "$case_dir/calls/python3"
case "\$2" in
  ready)
    if [ -f "$case_dir/no-pillow" ]; then
      echo "readme-captures.py needs Pillow: python3 -m pip install pillow" >&2
      exit 1
    fi
    exit 0 ;;
  check)
    asked=\$(grep -c '' "$case_dir/calls/python3")
    answer=\$(sed -n "\${asked}p" "$case_dir/checks")
    [ -n "\$answer" ] || answer=\$(tail -1 "$case_dir/checks")
    case "\$answer" in
      0) exit 0 ;;
      3) echo "\$3: a picture of nothing (2 colours under its status bar). Take it again" >&2; exit 3 ;;
      *) echo "\$3: cannot identify image file" >&2; exit 1 ;;
    esac ;;
esac
exit 0
EOF

  # xcrun: a screenshot written to the path it is handed, or refused with the simulator's words.
  cat > "$stubs/xcrun" <<EOF
#!/usr/bin/env bash
echo "\$*" >> "$case_dir/calls/xcrun"
if [ "\$1 \$2 \$4" = "simctl io screenshot" ]; then
  target="\${@: -1}"
  echo "\$target" >> "$case_dir/written-to"
  if [ -f "$case_dir/screenshot-refused" ]; then
    echo "An error was encountered processing the command (domain=NSCocoaErrorDomain, code=513): You don't have permission to save the file." >&2
    exit 1
  fi
  printf 'PNG of the Wallet' > "\$target"
fi
exit 0
EOF

  printf '#!/usr/bin/env bash\necho "$*" >> "%s/calls/dotnet"\nexit 0\n' "$case_dir" > "$stubs/dotnet"
  printf '#!/usr/bin/env bash\n[ "${1:-}" = -m ] && echo arm64 || echo Darwin\n' > "$stubs/uname"
  printf '#!/usr/bin/env bash\necho "$*" >> "%s/calls/sleep"\n' "$case_dir" > "$stubs/sleep"
  chmod +x "$stubs"/*
}

# Runs COMMAND... with the stubs first on PATH and capture-readme.sh's functions sourced, in a shell of
# its own, and keeps its exit and what it said.
captured() {
  (
    export PATH="$stubs:$PATH" EQ_README_RAW="$raw" TMPDIR="$case_dir/tmp"
    # shellcheck source=capture-readme.sh
    source "$root/scripts/capture-readme.sh"
    "$@"
  ) > "$case_dir/out" 2>&1
  status=$?
}

calls() { [ -f "$case_dir/calls/$1" ] && grep -c '' "$case_dir/calls/$1" || echo 0; }
said() { grep -qF -- "$1" "$case_dir/out"; }

# 1. No Pillow: the target stops before it builds anything, and says Pillow. The script is RUN here, not
#    sourced, since the question is asked where the targets are taken.
fixture no-pillow
touch "$case_dir/no-pillow"
(
  export PATH="$stubs:$PATH" EQ_README_RAW="$raw" TMPDIR="$case_dir/tmp"
  "$root/scripts/capture-readme.sh" ios
) > "$case_dir/out" 2>&1
status=$?
[ "$status" -ne 0 ] || failed "no Pillow: the ios target passed"
said "needs Pillow" || failed "no Pillow: the target did not say Pillow: $(cat "$case_dir/out")"
[ "$(calls dotnet)" -eq 0 ] || failed "no Pillow: the Wallet was built before Pillow was asked for"

# 2. The simulator refuses the screenshot: the target stops after one try, with the simulator's words.
fixture screenshot-refused
touch "$case_dir/screenshot-refused"
captured when_drawn "$raw/ios-dark-home.png" ios_photograph probe-udid
[ "$status" -ne 0 ] || failed "a refused screenshot: the capture passed"
said "You don't have permission to save the file" || failed "a refused screenshot: the simulator's words were not said: $(cat "$case_dir/out")"
[ "$(calls xcrun)" -eq 1 ] || failed "a refused screenshot: it was tried $(calls xcrun) times, where one try says why"

# 2b. The emulator refuses its screenshot: the same, with adb's words.
fixture emulator-refused
printf '#!/usr/bin/env bash\necho "$*" >> "%s/calls/adb"\n[ "$1" = exec-out ] || exit 0\necho "error: closed" >&2\nexit 1\n' "$case_dir" > "$stubs/adb"
chmod +x "$stubs/adb"
captured when_drawn "$raw/android-dark-cards.png" android_photograph
[ "$status" -ne 0 ] || failed "a refused emulator screenshot: the capture passed"
said "error: closed" || failed "a refused emulator screenshot: adb's words were not said: $(cat "$case_dir/out")"
[ "$(grep -c '^exec-out' "$case_dir/calls/adb")" -eq 1 ] \
  || failed "a refused emulator screenshot: it was tried $(grep -c '^exec-out' "$case_dir/calls/adb") times, where one try says why"

# 3. The screenshot is written where the simulator may write, and moved into the raw folder.
fixture written-then-moved
echo 0 > "$case_dir/checks"
captured when_drawn "$raw/ios-dark-home.png" ios_photograph probe-udid
[ "$status" -eq 0 ] || failed "a screenshot: the capture failed: $(cat "$case_dir/out")"
[ "$(cat "$raw/ios-dark-home.png" 2> /dev/null)" = "PNG of the Wallet" ] || failed "a screenshot: the raw folder holds no picture"
case "$(head -1 "$case_dir/written-to")" in
  "$case_dir/tmp/"*) ;;
  *) failed "a screenshot: the simulator wrote to $(head -1 "$case_dir/written-to"), not to the temporary directory" ;;
esac
[ -z "$(ls -A "$case_dir/tmp")" ] || failed "a screenshot: the temporary folder was left behind: $(ls -A "$case_dir/tmp")"

# 4. A check that refuses for a reason another picture would not cure: one try, and its reason.
fixture check-refused
echo 1 > "$case_dir/checks"
captured when_drawn "$raw/ios-dark-home.png" ios_photograph probe-udid
[ "$status" -ne 0 ] || failed "a refusing check: the capture passed"
said "cannot identify image file" || failed "a refusing check: its reason was not said: $(cat "$case_dir/out")"
[ "$(calls xcrun)" -eq 1 ] || failed "a refusing check: it was tried $(calls xcrun) times, where one try says why"

# 5. A picture of nothing is an app that has not drawn yet: it is waited out, and the capture passes.
fixture nothing-drawn
printf '3\n3\n0\n' > "$case_dir/checks"
captured when_drawn "$raw/ios-dark-home.png" ios_photograph probe-udid
[ "$status" -eq 0 ] || failed "a picture of nothing: the capture was not waited out: $(cat "$case_dir/out")"
[ "$(calls xcrun)" -eq 4 ] || failed "a picture of nothing: $(calls xcrun) screenshots, where three tries and the settled one are four"

# 6. The exit the check gives a picture of nothing is one number in both files.
in_shell="$(sed -n 's/^NOTHING_DRAWN=\([0-9]*\)$/\1/p' "$root/scripts/capture-readme.sh")"
in_python="$(sed -n 's/^NOTHING_DRAWN = \([0-9]*\)$/\1/p' "$root/scripts/readme-captures.py")"
[ -n "$in_shell" ] && [ "$in_shell" = "$in_python" ] \
  || failed "NOTHING_DRAWN is '${in_shell}' in capture-readme.sh and '${in_python}' in readme-captures.py"

if [ "$failures" -ne 0 ]; then
  echo "$failures case(s) failed" >&2
  exit 1
fi
echo "capture-readme: every case holds"
