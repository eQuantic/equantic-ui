- **2026-11-16 · Listing a device starts no tool of the SDK's**: the native SDK writes the Android icon just before
  Android collects its resources, and only when it is out of date, and the queries fragment runs only when the assembly
  changed. An InitialTarget wrote the icon in every invocation and the fragment ran on every deploy, so the workload's
  device listing and deploy built and started eqicon, and on a source tree
  `dotnet run --list-devices` and `--device` failed for want of `DOTNET_HOST_PATH`
  ([#1135](https://github.com/eQuantic/equantic-ui/issues/1135)). Proposed and archived through OpenSpec
  (`openspec/specs/app-icon`).
