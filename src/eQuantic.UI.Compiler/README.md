# eQuantic.UI.Compiler

The Roslyn-based compiler engine for **eQuantic.UI**.

## Overview

This tool is responsible for transpiling C# component code into optimized, zero-overhead JavaScript. It runs at build time, ensuring that your application starts fast and stays fast.

### Features

- **C# to JS Transpilation**: Converts C# logic, state management, and effects into efficient JavaScript.
- **Source Maps**: Generates full source maps for debugging C# directly in the browser.
- **Zero-runtime CSS**: Extracts styles defined in C# (via `StyleBuilder`) into static CSS files.
- **Tree Shaking**: Only compiles used components.

## Usage

This package is used internally by the `eQuantic.UI.Sdk` during the build process. You generally do not need to reference it directly.
