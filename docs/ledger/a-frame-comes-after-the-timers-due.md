- **2026-11-21 · A frame in the runtime's tests comes after the timers already due**: `test-runtime` failed
  three of six runs that day in a TreeView reload test, waiting thirty frames for a load that had not
  started. happy-dom runs a frame as an immediate, ahead of every timer, and a component's `Task.Yield`
  is a 0 ms timer: measured, a 0 ms timer fired after about 3,700 immediates under the embedded bun and
  about 76 under Node, so thirty frames bounded nothing whenever they held little work. The test
  environment now draws a frame after a 0 ms timer, as a browser runs the timers due before it updates
  the rendering, and its own spec fails when a frame outruns one. The wait that failed sees the load
  within one frame of the reload, 20 runs of 20, where it missed 5 of 20 before
  ([#1800](https://github.com/eQuantic/equantic-ui/issues/1800)).
