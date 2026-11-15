- **2026-11-12 · A module that constructs the build context imports its class**: a Bug under #1174
  ([#1475](https://github.com/eQuantic/equantic-ui/issues/1475)). eqc imported the build context under its
  annotation's name, `BuildContext`, for every reference, so a module that constructed one wrote
  `new ComponentContext(…)` with nothing to name, which `tsc` refused and the first build threw on. The
  scanner asks the bound tree how each name is used: a construction or a static member imports the class,
  `ComponentContext`, and an annotation the interface, as before. The element engine builds a context per
  position since #1412, and crosses to the browser in #1405.
