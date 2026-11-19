# Tasks

## 1. The proof

- [x] 1.1 A guard reads every shipped `.props` and `.targets` and fails when an InitialTarget, or a target it depends on, starts a program or builds a project: the Android icon did
- [x] 1.2 Guards fail unless the Android icon runs before `_ComputeAndroidResourcePaths`, and unless every Android target that starts a program has inputs and outputs: the queries fragment had none
- [x] 1.3 A cold Android build of the Wallet sample, from no generated assets, writes the icon and packages it, and the next build skips the icon target
- [x] 1.4 `dotnet run --list-devices` and `dotnet run --device` on the source tree, with no `DOTNET_HOST_PATH`, list and deploy, and start no eqicon

## 2. The cure

- [x] 2.1 The Android icon target runs before `_ComputeAndroidResourcePaths`, incremental, and builds no edge itself
- [x] 2.2 The queries fragment runs only when the assembly or eqicon changed
