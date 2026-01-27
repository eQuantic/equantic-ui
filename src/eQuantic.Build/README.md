# eQuantic.Build

MSBuild tasks and targets for the **eQuantic** ecosystem.

## Overview

This package contains the low-level build infrastructure used by `eQuantic.UI.Sdk` and other eQuantic tools. It handles:

- **Task Execution**: Running custom MSBuild tasks during compilation.
- **Asset Processing**: Managing static assets.
- **Tool Resolution**: Locating external tools (like Node.js/Bun) required for the build.
