# eQuantic.UI.Runtime

The client-side JavaScript runtime for **eQuantic.UI**.

## Overview

This lightweight runtime (<30kb) is responsible for:

- **Hydration**: Attaching behavior to server-rendered HTML.
- **State Management**: Handling `SetState` calls and updating the DOM efficiently.
- **Event Handling**: Proxying DOM events to C# logic (via compiled JS).
- **Network**: Managing `Server Actions` calls via RPC.

## Usage

This package is automatically bundled and managed by the `eQuantic.UI.Sdk`.
