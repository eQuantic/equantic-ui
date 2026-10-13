#!/usr/bin/env bash
# Launches the Wallet sample on an iOS simulator and fails unless it DREW (#796).
#
# A build proves nothing here: an app whose shell stops connecting its scene builds green and shows a
# black screen, and one built with the iOS 27 SDK without the scene life cycle does not start at all.
# So the app is launched with a frame limit and the line it prints after its last frame is read from
# the simulator's unified log. That line exists only when the app ran and presented its frames.
#
# The simulator is a fresh one, made on the newest iOS runtime installed (or EQ_IOS_RUNTIME, a runtime
# identifier) and deleted on the way out, so a developer's own booted simulator is never touched.
#
# And what the app DECLARED in C# has to be in the bundle (#777): the SDK writes a partial manifest
# from the compiled assembly, with the sentence behind each permission and the URL schemes, and
# Apple's packaging merges it only when it is handed over in time. Handed over late, the build is
# green, the file is there, and the Info.plist ships without a key the app said it needed.
#
#   ./scripts/check-ios-launch.sh            # build the sample in Release, then launch it
#   EQ_IOS_APP=<path>.app ./scripts/...      # launch an app that is already built
#   EQ_IOS_DECLARED=<path>.plist             # with EQ_IOS_APP: the partial manifest that app's build wrote
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
frames=60

app=${EQ_IOS_APP:-}
declared=${EQ_IOS_DECLARED:-}
if [ -z "$app" ]; then
  # Release: a Debug build installs a .app without its assemblies, which crashes on launch. The
  # simulator runs the machine's own architecture.
  case "$(uname -m)" in
    arm64) rid=iossimulator-arm64 ;;
    *) rid=iossimulator-x64 ;;
  esac
  dotnet build "$root/samples/WalletMobile" -f net10.0-ios -r "$rid" -c Release
  # Read back from what the build wrote, rather than a path spelled here.
  app=$(find "$root/samples/WalletMobile/bin/Release" -maxdepth 4 -path "*/$rid/*" -name "*.app" | head -1)
  # The sample declares what it asks of the device, so its build writes this file.
  declared="$root/samples/WalletMobile/obj/eQuantic/Capabilities.plist"
fi
if [ ! -f "$app/Info.plist" ]; then
  echo "::error::no app at $app, so this would launch nothing"
  exit 1
fi

plist() { /usr/libexec/PlistBuddy -c "Print :$1" "$app/Info.plist" 2>/dev/null || true; }
bundle=$(plist CFBundleIdentifier)
process=$(plist CFBundleExecutable)
echo "App: $app ($bundle), linked against $(plist DTSDKName)."

# What the SDK merged into the Info.plist, read back from the bundle rather than from the SDK's file.
if [ "$(plist UIApplicationSceneManifest:UIApplicationSupportsMultipleScenes)" != "false" ]; then
  echo "::error::$app/Info.plist declares no scene manifest with one scene"
  exit 1
fi

# Every key the SDK wrote from the app's own declarations, compared with what the bundle carries.
# Derived from the partial manifest, never listed here: a key the sample starts declaring is checked
# the day it is declared.
if [ -n "$declared" ]; then
  if [ ! -f "$declared" ]; then
    echo "::error::no partial manifest at $declared, so nothing the app declared was checked"
    exit 1
  fi
  missing=$(jq -rn \
    --argjson declared "$(plutil -convert json -o - "$declared")" \
    --argjson built "$(plutil -convert json -o - "$app/Info.plist")" \
    '$declared | to_entries[] | select($built[.key] != .value) | .key')
  if [ -n "$missing" ]; then
    echo "::error::$app/Info.plist does not carry what the app declared. Written to $declared and not merged:"
    echo "$missing"
    exit 1
  fi
  echo "Declared and in the Info.plist: $(plutil -convert json -o - "$declared" | jq -r 'keys | join(", ")')."
fi

runtime=${EQ_IOS_RUNTIME:-$(xcrun simctl list runtimes --json | jq -r '
  [.runtimes[] | select(.platform == "iOS" and .isAvailable)]
  | sort_by(.version | split(".") | map(tonumber)) | last | .identifier // empty')}
if [ -z "$runtime" ]; then
  echo "::error::no iOS simulator runtime is installed"
  exit 1
fi
# The runtime lists the phones it supports newest first.
devicetype=$(xcrun simctl list runtimes --json | jq -r --arg runtime "$runtime" '
  [.runtimes[] | select(.identifier == $runtime) | .supportedDeviceTypes[]
   | select(.productFamily == "iPhone") | .identifier] | first // empty')
if [ -z "$devicetype" ]; then
  echo "::error::$runtime supports no iPhone"
  exit 1
fi

udid=$(xcrun simctl create "eq-ios-launch-$$" "$devicetype" "$runtime")
trap 'xcrun simctl shutdown "$udid" >/dev/null 2>&1 || true; xcrun simctl delete "$udid" >/dev/null 2>&1 || true' EXIT
echo "Simulator: $udid ($devicetype on $runtime)."
xcrun simctl boot "$udid"
xcrun simctl bootstatus "$udid" -b >/dev/null
xcrun simctl install "$udid" "$app"

start=$(date "+%Y-%m-%d %H:%M:%S")
SIMCTL_CHILD_Photon__MaxFrames=$frames xcrun simctl launch "$udid" "$bundle"

app_log() {
  xcrun simctl spawn "$udid" log show --start "$start" --style compact \
    --predicate "process == \"$process\"" 2>/dev/null || true
}

# Whether the app's process is still there. An app that crashed at launch, the defect this step
# exists for, is reported now rather than after the whole wait.
running() {
  xcrun simctl spawn "$udid" launchctl list 2>/dev/null \
    | awk -v label="UIKitApplication:$bundle[" 'index($3, label) == 1 && $1 != "-" { found = 1 } END { exit !found }'
}

# The line the controller prints after its last frame, and only when nothing was CONTAINED and no
# face went missing: those it appends to the same line, so the line is matched to its end. Polled,
# because a cold simulator takes its time. Matched on a here-string, never `| grep -q`: under
# pipefail a grep that stops at the first match can fail the pipeline it stopped, and read a found
# line as a missing one.
done_line="\[photon\] frames presented: $frames\$"
log=""
for _ in $(seq 1 120); do
  log=$(app_log)
  if grep -qE "\[photon\] frames presented: " <<<"$log"; then break; fi
  if ! running; then
    sleep 2
    log=$(app_log)
    echo "::error::$bundle is no longer running on $runtime."
    break
  fi
  sleep 2
done

grep -F "[photon]" <<<"$log" || true

if ! grep -qE "$done_line" <<<"$log"; then
  echo "::error::$bundle did not present $frames clean frames on $runtime. The last of its log:"
  tail -40 <<<"$log"
  exit 1
fi
if grep -qF "no UIScene configuration" <<<"$log"; then
  echo "::error::UIKit could not find the scene configuration the app delegate named:"
  grep -F "no UIScene configuration" <<<"$log"
  exit 1
fi
echo "$bundle presented $frames frames on $runtime under the scene life cycle."
