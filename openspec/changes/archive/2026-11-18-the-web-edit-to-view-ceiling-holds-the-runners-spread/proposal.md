## Why

The roadmap's bar is an edit on screen in under a second, and #900 measured it on both samples. The
desktop met it from the start; the web was held to a ceiling of 35.5s that only came down. #1364 and
#1380 took eqc's part of a save to the files it can have changed, and the web's warm median over six of
main's samples jobs was 479 to 1053ms: under the bar on five of them. The ceiling still reads 35.5s,
and the run kept asking for it to come down.

## What Changes

- The web's ceiling is 1.5s: the runner's spread over the bar. A ceiling of a second would have failed
  the run that measured 1053ms.
- Once a ceiling is within one and a half bars, the run says whether the warm median met the bar
  instead of asking for the ceiling to come down. Above that it still asks, as before.
- The roadmap says what was measured: the desktop holds the bar, and the web meets it on most runs
  and not on all.

## Capabilities

### New Capabilities

### Modified Capabilities

- `hot-reload`: the web's ceiling, and what the run says within the runner's spread of the bar.

## Impact

- **Public surface**: none. The developer surface does not move.
- **CI**: the samples job fails a web warm median over 1.5s.
