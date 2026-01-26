# eQuantic UI VS Code Extension

Simple extension providing snippets for eQuantic UI development.

## Installation

1. Install `vsce` if needed: `npm install -g vsce`
2. Package the extension: `vsce package`
3. Install the `.vsix` file in VS Code: `code --install-extension equantic-ui-vscode-0.1.0.vsix`

## Features

### Snippets (C#)

- `eq-page`: Generates a `StatefulComponent` with `Page` attribute and State class.
- `eq-comp`: Generates a `StatelessComponent`.
- `eq-style`: Generates a `Style` class.

## Debugging

To debug your C# components running in the CLI dev server, ensure your `launch.json` is configured to attach to the .NET process.

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Attach",
            "type": "coreclr",
            "request": "attach"
        }
    ]
}
```
