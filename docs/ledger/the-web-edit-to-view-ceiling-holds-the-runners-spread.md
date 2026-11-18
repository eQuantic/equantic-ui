- **2026-11-18 · Edit-to-view is held to the bar on the desktop, and to the runner's spread over it
  on the web**: with eqc compiling only what a save can have changed (#1364, #1380), the web's warm
  median from a save to the frame ran 479 to 1053ms over six of main's samples jobs, under the bar of
  a second on five; the desktop holds it near 200ms. The web's ceiling comes down from 35.5s to 1.5s,
  the runner's spread over the bar, and within that spread the run says whether the median met the
  bar instead of asking for the ceiling to come down
  ([#900](https://github.com/eQuantic/equantic-ui/issues/900)).
