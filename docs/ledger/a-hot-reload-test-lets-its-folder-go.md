- **2026-11-10 · A hot-reload test stops its app before it removes the app's folder**: a Bug under #1174
  ([#1366](https://github.com/eQuantic/equantic-ui/issues/1366)). `HotReloadDecisionTests` disposed its app
  without stopping it, so hot reload, which is disposed when the app stops, kept its watchers on the content
  root and the MSBuild the running compiler asks, whose working directory is that root, and Windows refused
  to remove the folder: `test (windows-latest, rest)` went red now and then on pull requests that touch no
  hot-reload code. The cases now stop their app before disposing it.
