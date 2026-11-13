#!/usr/bin/env bash
# Runs the desktop sample for a number of frames and fails if a frame leaves an object behind (#1473).
#
# The macOS window runs its own event loop, and a loop a shell runs itself has no autorelease pool
# unless it brings one. Without it, what each turn was handed autoreleased lived as long as the process:
# one drawable, one command buffer, one render pass descriptor and the GPU's render context per frame,
# measured, 482 of each after 480 frames. Nothing failed, and nothing would: the window drew, the tests
# passed, and the memory grew by a frame's worth with every frame.
#
# The Objective-C runtime can say so itself. With OBJC_DEBUG_MISSING_POOLS set, every object
# autoreleased where no pool is in place is reported, by thread and by class, instead of being kept
# quietly. The drawable and the render pass descriptor are what every frame takes and no startup does,
# so the rule is exact: on the loop's thread, a run that presented its frames reports none of them. Each
# turn of the loop draining its pool makes that zero; a turn without one makes it one of each per frame.
#
# The loop's thread is the MAIN thread, told apart as the one that reports an NSWindow: the frameworks'
# own dispatch queues report their objects under this flag too, and a drawable autoreleased there is
# not this loop's. Both class names are matched loosely (any class ending in Drawable, any render pass
# descriptor), so a GPU or a macOS that names its drawable otherwise is still counted, and a run where
# the main thread reports neither the window nor any per-frame class at all is not taken as a pass.
#
# The startup is reported too, and that is not a defect of ours: the frameworks the app loads before
# its loop starts (AppKit, CoreMediaIO) autorelease on the main thread and on their own queues before
# any pool exists. It is also the proof the flag was read, so a run that reports nothing at all fails:
# a runtime that ignored the flag would otherwise pass this having proved nothing.
#
#   ./scripts/check-frame-pools.sh                  # build the desktop sample, then run it
#   EQ_DESKTOP_APP=<path> ./scripts/...             # run a sample that is already built
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
frames=120

app=${EQ_DESKTOP_APP:-}
if [ -z "$app" ]; then
  dotnet build "$root/samples/PhotonDesktop"
  # Read back from what the build wrote, rather than a path spelled here.
  app=$(find "$root/samples/PhotonDesktop/bin/Debug" -maxdepth 2 -type f -name PhotonDesktop -perm -u+x | head -1)
fi
if [ ! -x "$app" ]; then
  echo "::error::no desktop sample at '$app', so this would run nothing"
  exit 1
fi

log=$(mktemp)
trap 'rm -f "$log"' EXIT

# macOS has no timeout(1): perl's alarm ends a window that never reaches its frame count, so the step
# fails on the count below instead of hanging the job.
OBJC_DEBUG_MISSING_POOLS=YES perl -e 'alarm 180; exec @ARGV' "$app" --Photon:MaxFrames "$frames" > "$log" 2>&1 || {
  echo "::error::the desktop sample exited with $? before presenting $frames frames"
  grep -v 'MISSING POOLS' "$log" | tail -20
  exit 1
}

# The line the window prints after its last frame. It exists only when the loop ran that far.
presented=$(sed -n 's/.*\[photon\] frames presented: \([0-9]*\).*/\1/p' "$log" | tail -1)
if [ -z "$presented" ] || [ "$presented" -lt "$frames" ]; then
  echo "::error::the desktop sample presented ${presented:-no} frames of the $frames asked for"
  grep -v 'MISSING POOLS' "$log" | tail -20
  exit 1
fi

reported=$(grep -c 'MISSING POOLS' "$log" || true)
if [ "$reported" -eq 0 ]; then
  echo "::error::OBJC_DEBUG_MISSING_POOLS reported nothing, not even the startup's frameworks, so the runtime did not read it and this proves nothing"
  exit 1
fi

# The thread that reports the window is the one the loop runs on.
main=$(sed -n 's/.*MISSING POOLS: (\(0x[0-9a-f]*\)) Object [^ ]* of class NSWindow .*/\1/p' "$log" | sort | uniq -c | sort -rn | awk 'NR==1 {print $2}')
if [ -z "$main" ]; then
  echo "::error::no thread reported an NSWindow, so the loop's thread could not be told apart and this proves nothing"
  exit 1
fi

per_frame='of class ([A-Za-z_]*Drawable|MTLRenderPassDescriptor[A-Za-z]*) '
left=$(grep -F "MISSING POOLS: ($main)" "$log" | grep -cE "$per_frame" || true)
echo "Presented $presented frames. The runtime reported $reported objects autoreleased with no pool; on the loop's thread ($main), $left of them were a frame's drawable or render pass descriptor."
if [ "$left" -ne 0 ]; then
  echo "::error::$left objects a frame takes were left behind over $presented frames: the loop turns without an autorelease pool"
  echo "What the loop's thread left, by class:"
  grep -F "MISSING POOLS: ($main)" "$log" | sed -n 's/.* of class \([^ ]*\) .*/\1/p' | sort | uniq -c | sort -rn | head -10
  exit 1
fi
