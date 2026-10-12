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
#   ./scripts/check-ios-launch.sh            # build the sample in Release, then launch it
#   EQ_IOS_APP=<path>.app ./scripts/...      # launch an app that is already built
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
frames=60

app=${EQ_IOS_APP:-}
if [ -z "$app" ]; then
  # Release: a Debug build installs a .app without its assemblies, which crashes on launch.
  dotnet build "$root/samples/WalletMobile" -f net10.0-ios -r iossimulator-arm64 -c Release
  app="$root/samples/WalletMobile/bin/Release/net10.0-ios/iossimulator-arm64/WalletMobile.app"
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

# The line the controller prints after its last frame. Polled: a cold simulator takes its time.
# Matched on a here-string, never `| grep -q`: under pipefail a grep that stops at the first match
# can fail the pipeline it stopped, and read a found line as a missing one.
done_line="[photon] frames presented: $frames"
log=""
for _ in $(seq 1 120); do
  log=$(app_log)
  if grep -qF "$done_line" <<<"$log"; then break; fi
  sleep 2
done

grep -F "[photon]" <<<"$log" || true

if ! grep -qF "$done_line" <<<"$log"; then
  echo "::error::$bundle did not present $frames frames on $runtime. The last of its log:"
  tail -40 <<<"$log"
  exit 1
fi
if grep -qF "no UIScene configuration" <<<"$log"; then
  echo "::error::UIKit could not find the scene configuration the app delegate named:"
  grep -F "no UIScene configuration" <<<"$log"
  exit 1
fi
echo "$bundle presented $frames frames on $runtime under the scene life cycle."
