- **2026-10-29 · The VS Code extension's packaging tooling moves past sixteen advisories**: a task under #306
  ([#1178](https://github.com/eQuantic/equantic-ui/issues/1178)). Dependabot reported seventeen open advisories
  against the extension's lockfile, all in the tooling that packages it (`@vscode/vsce` and what it pulls), none
  in the `.vsix`, which carries no `node_modules`. Sixteen closed inside the declared ranges: `undici` 7.30.1,
  `markdown-it` 14.3.3, `fast-uri` 3.1.9 (its override's floor raised to the patched version) and
  `brace-expansion` 5.0.12, and no other package moved. Proved by the job that reads the lock, `npm ci` and
  `npm run package`, whose check spawns the packaged design host. The seventeenth, `@azure/msal-node`, waits on an
  `@azure/identity` that accepts MSAL 7 (#1179).
