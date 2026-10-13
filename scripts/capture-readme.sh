#!/usr/bin/env bash
# The README's pictures, taken from the samples as they build (#880).
#
#   ./scripts/capture-readme.sh web        the dashboard sample in a headless Chrome
#   ./scripts/capture-readme.sh desktop    the component gallery, drawn by Photon with no window
#   ./scripts/capture-readme.sh ios        the Wallet sample on an iPhone simulator of its own
#   ./scripts/capture-readme.sh android    the Wallet sample on the Android emulator that is running
#   ./scripts/capture-readme.sh compose    the raw captures made into assets/readme/
#   ./scripts/capture-readme.sh all        every one of them, in that order
#
# A picture in a README goes stale in silence: the screen it shows changes and the picture stays. So
# the pictures are captures a script takes, and assets/readme/captures.json names the files each one
# was taken from. ReadmeCapturesTests fails when one of those files changes. Then: take the picture
# again with the target that file belongs to, LOOK at it, and record the new state with
# EQ_UPDATE_README_CAPTURES=1.
#
# Nothing has to be clicked. A screen is chosen by configuration: a route for the dashboard,
# `--section` for the gallery, `screen` for the Wallet. Raw captures go to obj/readme-captures, which
# git ignores, and a capture that drew nothing is refused where it is taken.
#
# What each target needs: `web` a Google Chrome (EQ_CHROME names another), `desktop` and `ios` a Mac,
# `ios` an iOS simulator runtime, `android` an emulator already booted, and every one a python3 with
# Pillow, which reads each capture. The emulator is its owner's: `android` replaces no Wallet it
# finds there, and photographs one only when EQ_README_ANDROID_INSTALLED=1 says it is this tree's.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RAW="${EQ_README_RAW:-$ROOT/obj/readme-captures}"
OUT="$ROOT/assets/readme"
mkdir -p "$RAW"

fail() {
    echo "capture-readme: $1" >&2
    exit 1
}

# What a target started is undone when it ends and when the script dies, last first: a server, a
# browser's profile, a simulator, an emulator's settings.
UNDO=()
undo() {
    local index
    for ((index = ${#UNDO[@]} - 1; index >= 0; index--)); do
        eval "${UNDO[$index]}" > /dev/null 2>&1 || true
    done
    UNDO=()
}
trap undo EXIT

lower() {
    echo "$1" | tr '[:upper:]' '[:lower:]'
}

# A capture that drew nothing is refused here, not found later in the README.
drew() {
    python3 "$ROOT/scripts/readme-captures.py" check "$1"
    echo "  $(basename "$1")"
}

# when_drawn <picture> <command that takes it...>: the picture, taken once the app has drawn.
#
# How long an app takes to its first frame is not a number to wait for. On a simulator just booted
# the Wallet stayed black for more than twenty seconds, after a launch spent to warm it up. So the
# screen is photographed until it holds a picture, for up to a minute, and then once more after two
# seconds, when what slides in has settled.
when_drawn() {
    local out="$1" seconds=0
    shift
    until "$@" "$out" && python3 "$ROOT/scripts/readme-captures.py" check "$out" 2> /dev/null; do
        sleep 2
        seconds=$((seconds + 2))
        [ "$seconds" -lt 60 ] || break
    done
    sleep 2
    "$@" "$out"
    drew "$out"
}

# web_picture <chrome> <profile> <port> <scheme> <route>
web_picture() {
    local chrome="$1" profile="$2" port="$3" scheme="$4" route="$5" preferred=1 name="${5#/}"
    # preferredColorScheme: 0 is dark and 1 is light, and the dashboard follows it.
    [ "$scheme" = dark ] && preferred=0
    local out="$RAW/web-$scheme-${name:-payments}.png"
    rm -f "$out"
    "$chrome" --headless=new --disable-gpu --hide-scrollbars --no-first-run --no-default-browser-check \
        --user-data-dir="$profile" --blink-settings=preferredColorScheme="$preferred" \
        --window-size=1440,900 --force-device-scale-factor=2 --virtual-time-budget=7000 \
        --screenshot="$out" "http://127.0.0.1:$port$route" > "$RAW/chrome.log" 2>&1 &
    local browser=$! seconds=0
    # A page that keeps a connection open never finishes loading, and Chrome then never writes its
    # picture: /live held one for ten minutes. Forty seconds, and the browser is stopped.
    while kill -0 "$browser" 2> /dev/null; do
        sleep 1
        seconds=$((seconds + 1))
        if [ "$seconds" -ge 40 ]; then
            kill "$browser" 2> /dev/null || true
            fail "$route did not finish loading in 40 seconds, so there is no picture of it"
        fi
    done
    drew "$out"
}

web() {
    local chrome="${EQ_CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
    [ -x "$chrome" ] || fail "no Chrome at $chrome. EQ_CHROME names another"
    local port="${EQ_README_PORT:-5396}" sample="$ROOT/samples/DefaultUIDashboard" profile
    profile="$(mktemp -d)"
    UNDO+=("rm -rf '$profile'")

    dotnet build "$sample" -v:q -nologo
    # A server that is there already would answer in the dashboard's place, and its pages would be
    # photographed as the dashboard's.
    if curl -s -o /dev/null "http://127.0.0.1:$port/"; then
        fail "something already answers on port $port. Stop it, or name another port with EQ_README_PORT"
    fi
    # Production: a page in Development keeps a hot reload connection open.
    (cd "$sample" && exec dotnet bin/Debug/net10.0/DefaultUIDashboard.dll --environment Production \
        --contentRoot "$sample" --urls "http://127.0.0.1:$port") > "$RAW/dashboard.log" 2>&1 &
    local server=$! waited=0
    UNDO+=("kill $server")
    until curl -s -o /dev/null "http://127.0.0.1:$port/"; do
        kill -0 "$server" 2> /dev/null || fail "the dashboard did not start: see $RAW/dashboard.log"
        sleep 1
        waited=$((waited + 1))
        [ "$waited" -lt 60 ] || fail "the dashboard did not answer in 60 seconds"
    done
    kill -0 "$server" 2> /dev/null || fail "the dashboard stopped: see $RAW/dashboard.log"

    local route
    for route in / /charts /sheet /code /diff /markdown /form /rows /i18n /colors; do
        web_picture "$chrome" "$profile" "$port" dark "$route"
    done
    web_picture "$chrome" "$profile" "$port" light /form
    undo
}

# desktop_frame <sample> <mode> <section>
desktop_frame() {
    local sample="$1" mode="$2" section="$3"
    local out; out="$RAW/desktop-$(lower "$mode")-$(lower "$section").png"
    rm -f "$out"
    # --Photon:ScreenshotPath draws one settled frame with no window and no GPU, and exits.
    (cd "$sample/bin/Debug/net10.0" && dotnet PhotonDesktop.dll --Photon:ScreenshotPath "$out" \
        --Photon:Mode "$mode" --section "$section" --culture en-US) > "$RAW/desktop.log" 2>&1
    drew "$out"
}

desktop() {
    [ "$(uname -s)" = Darwin ] || fail "the gallery's picture is drawn by the macOS shell"
    local sample="$ROOT/samples/PhotonDesktop"
    dotnet build "$sample" -v:q -nologo
    desktop_frame "$sample" Dark Charts
    desktop_frame "$sample" Light Buttons
}

# ios_screen <udid> <bundle> <appearance> <screen>
ios_screen() {
    local udid="$1" bundle="$2" appearance="$3" name="$4"
    local out; out="$RAW/ios-$appearance-$(lower "$name").png"
    rm -f "$out"
    xcrun simctl ui "$udid" appearance "$appearance"
    xcrun simctl terminate "$udid" "$bundle" > /dev/null 2>&1 || true
    # Configuration travels as environment on iOS, and SIMCTL_CHILD_<name> sets it. A simulator that
    # has only just booted refuses a launch ("did not return a process handle"), so it is asked
    # until it takes one.
    local tries=0
    until SIMCTL_CHILD_screen="$name" xcrun simctl launch "$udid" "$bundle" > /dev/null 2>&1; do
        tries=$((tries + 1))
        [ "$tries" -lt 20 ] || fail "the simulator did not launch $bundle in a minute"
        sleep 3
    done
    when_drawn "$out" ios_photograph "$udid"
}

ios_photograph() {
    xcrun simctl io "$1" screenshot --type=png "$2" > /dev/null 2>&1
}

ios() {
    [ "$(uname -s)" = Darwin ] || fail "an iOS simulator needs a Mac"
    local rid=iossimulator-x64
    [ "$(uname -m)" = arm64 ] && rid=iossimulator-arm64
    # Release: a Debug build installs an app without its assemblies.
    dotnet build "$ROOT/samples/WalletMobile" -f net10.0-ios -r "$rid" -c Release -v:q -nologo
    local app bundle runtime devicetype udid
    app="$(find "$ROOT/samples/WalletMobile/bin/Release" -maxdepth 4 -path "*/$rid/*" -name '*.app' | head -1)"
    [ -f "$app/Info.plist" ] || fail "no app was built for $rid"
    bundle="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$app/Info.plist")"

    # A simulator of its own, as scripts/check-ios-launch.sh makes one: the newest iOS runtime and
    # the newest iPhone it supports, deleted on the way out. A developer's own is never touched.
    runtime="$(xcrun simctl list runtimes --json | python3 -c '
import json, sys
found = [r for r in json.load(sys.stdin)["runtimes"] if r.get("platform") == "iOS" and r.get("isAvailable")]
found.sort(key=lambda r: [int(part) for part in r["version"].split(".")])
print(found[-1]["identifier"] if found else "")')"
    [ -n "$runtime" ] || fail "no iOS simulator runtime is installed"
    devicetype="$(xcrun simctl list runtimes --json | python3 -c '
import json, sys
for runtime in json.load(sys.stdin)["runtimes"]:
    if runtime["identifier"] == sys.argv[1]:
        phones = [t["identifier"] for t in runtime["supportedDeviceTypes"] if t.get("productFamily") == "iPhone"]
        print(phones[0] if phones else "")' "$runtime")"
    [ -n "$devicetype" ] || fail "$runtime supports no iPhone"
    udid="$(xcrun simctl create "eq-readme-$$" "$devicetype" "$runtime")"
    UNDO+=("xcrun simctl delete $udid" "xcrun simctl shutdown $udid")

    xcrun simctl boot "$udid"
    xcrun simctl bootstatus "$udid" -b > /dev/null
    xcrun simctl status_bar "$udid" override --time "9:41" --batteryState charged --batteryLevel 100 \
        --cellularBars 4 --wifiBars 3
    xcrun simctl install "$udid" "$app"

    ios_screen "$udid" "$bundle" dark Home
    ios_screen "$udid" "$bundle" light Detail
    undo
}

# android_screen <package> <activity> <screen>
android_screen() {
    local package="$1" activity="$2" name="$3"
    local out; out="$RAW/android-dark-$(lower "$name").png"
    rm -f "$out"
    adb shell am force-stop "$package"
    # A Debug build reads debug.mono.env as its environment.
    adb shell "setprop debug.mono.env 'screen=$name'"
    adb shell am start -n "$activity" > /dev/null 2>&1
    when_drawn "$out" android_photograph
}

# A screen that is off photographs as black, and an emulator booted with no window is asleep. Each
# photograph wakes it, which also keeps it from going back to sleep while an app takes its time.
android_photograph() {
    adb shell input keyevent KEYCODE_WAKEUP
    adb exec-out screencap -p > "$1"
}

android_demo() {
    adb shell am broadcast -a com.android.systemui.demo "$@" > /dev/null
}

# quoted <text>: the text as ONE word for the shell `adb shell` hands its command to. What is put
# back was read from the device, and debug.mono.env is a list its developer wrote with `|` between
# the names, which that shell would take for a pipe.
quoted() {
    printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"
}

# The emulator as android() found it, put back from what it read before it changed anything.
android_restore() {
    adb shell am force-stop "$ANDROID_PACKAGE"
    android_demo -e command exit
    adb shell "cmd uimode night $(quoted "$ANDROID_NIGHT")"
    adb shell "setprop debug.mono.env $(quoted "$ANDROID_ENVIRONMENT")"
    if [ "$ANDROID_DEMO" = null ]; then
        adb shell settings delete global sysui_demo_allowed
    else
        adb shell "settings put global sysui_demo_allowed $(quoted "$ANDROID_DEMO")"
    fi
    case "$ANDROID_AWAKE" in
        Asleep | Dozing) adb shell input keyevent KEYCODE_SLEEP ;;
    esac
}

android() {
    command -v adb > /dev/null || fail "no adb on the PATH"
    [ "$(adb get-state 2> /dev/null)" = device ] || fail "no Android emulator is running. Boot one with a GPU the app can present through (-gpu host)"
    local sample="$ROOT/samples/WalletMobile" apk there version answer activity
    ANDROID_PACKAGE=tech.equantic.wallet

    # What is on the emulator is its owner's. A Wallet already there is never replaced: it is
    # photographed when the caller says it is this tree's build (EQ_README_ANDROID_INSTALLED=1),
    # which is also the way through for an emulator short of storage, since that one refuses an
    # install it has room for and what to do about it is its owner's to say. A Wallet this
    # installs, it uninstalls.
    there="$(adb shell pm path "$ANDROID_PACKAGE" 2> /dev/null | tr -d '\r' || true)"
    if [ -n "$there" ]; then
        version="$(adb shell dumpsys package "$ANDROID_PACKAGE" | tr -d '\r' | sed -n 's/^ *versionName=//p' | head -1 || true)"
        [ -n "${EQ_README_ANDROID_INSTALLED:-}" ] || fail "$ANDROID_PACKAGE is on this emulator already (version ${version:-unknown}), and this replaces nothing its owner installed. Uninstall it and this installs the build of this tree, or say EQ_README_ANDROID_INSTALLED=1 when it IS the build of this tree, to photograph it as it is"
    else
        [ -z "${EQ_README_ANDROID_INSTALLED:-}" ] || fail "EQ_README_ANDROID_INSTALLED=1 says the Wallet is on the emulator already, and $ANDROID_PACKAGE is not there"
        # The assemblies inside the package: a Debug build leaves them out for a fast deployment,
        # and that package starts nothing when it is installed by hand.
        dotnet build "$sample" -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true -v:q -nologo
        apk="$(ls -t "$sample"/bin/Debug/net10.0-android/*-Signed.apk | head -1)"
        UNDO+=("adb uninstall $ANDROID_PACKAGE")
        answer="$(adb install "$apk" 2>&1 | grep -m1 -E 'Success|Failure' || true)"
        case "$answer" in
            Success*) ;;
            *) fail "the emulator refused the install: ${answer:-no answer}" ;;
        esac
    fi

    # And its settings are left as they were found: its night mode, whether its status bar may be
    # staged, the property that names a screen, which is a developer's own list of variables, and
    # a screen that was off.
    ANDROID_AWAKE="$(adb shell dumpsys power | tr -d '\r' | sed -n 's/^ *mWakefulness=//p' | head -1 || true)"
    ANDROID_NIGHT="$(adb shell cmd uimode night | tr -d '\r' | sed -E 's/.*: //')"
    ANDROID_ENVIRONMENT="$(adb shell getprop debug.mono.env | tr -d '\r')"
    ANDROID_DEMO="$(adb shell settings get global sysui_demo_allowed | tr -d '\r')"
    UNDO+=("android_restore")
    activity="$(adb shell cmd package resolve-activity --brief "$ANDROID_PACKAGE" | tail -1 | tr -d '\r')"

    adb shell input keyevent KEYCODE_WAKEUP
    adb shell wm dismiss-keyguard
    adb shell cmd uimode night yes > /dev/null
    adb shell settings put global sysui_demo_allowed 1
    android_demo -e command enter
    android_demo -e command clock -e hhmm 0941
    android_demo -e command battery -e level 100 -e plugged false
    android_demo -e command network -e wifi show -e level 4
    android_demo -e command network -e mobile show -e datatype none -e level 4
    android_demo -e command notifications -e visible false

    android_screen "$ANDROID_PACKAGE" "$activity" Transactions
    android_screen "$ANDROID_PACKAGE" "$activity" Cards
    undo
}

compose() {
    python3 "$ROOT/scripts/readme-captures.py" compose "$RAW" "$OUT"
}

if [ "$#" -eq 0 ]; then
    sed -n '3,9p' "$0" | sed -E 's/^# ?//'
    exit 2
fi
for target in "$@"; do
    case "$target" in
        web | desktop | ios | android | compose)
            echo "$target:"
            "$target"
            ;;
        all)
            for each in web desktop ios android compose; do
                echo "$each:"
                "$each"
            done
            ;;
        *) fail "no target named $target. One of: web, desktop, ios, android, compose, all" ;;
    esac
done
