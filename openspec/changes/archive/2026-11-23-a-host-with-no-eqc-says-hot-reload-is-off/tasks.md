# Tasks

## 1. The proof

- [x] 1.1 A test asks the real MSBuild about a real `Microsoft.NET.Sdk.Web` project: hot reload is said to be off once, with no MSB4057, no "Build failed" and no "could not say how to start eqc", and a second compile asks MSBuild no more. On the code before the cure the log held MSBuild's error three times
- [x] 1.2 A test saves in a host whose compiler has no eqc: nothing is rebuilt, said or sent. On the code before the cure it said it was rebuilding
- [x] 1.3 The dashboard sample, an eQuantic.UI app, still starts its eqc and compiles through the same discovery

## 2. The cure

- [x] 2.1 The discovery asks whether the project is built with eQuantic.UI.Sdk only when MSBuild refuses the SDK's target, and remembers a no
- [x] 2.2 The hot reload service rebuilds nothing, says nothing and watches the sources no more once the compiler has no eqc
