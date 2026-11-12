- **2026-11-10 · Hot reload ends with its host, stopped or not**: a Bug under #1174
  ([#1375](https://github.com/eQuantic/equantic-ui/issues/1375)). Hot reload was disposed only when the app stopped,
  and disposing a host does not stop it, so a host disposed without being stopped kept watching its content root and
  kept its compiler, whose working directory is that root: a save there still started a rebuild, and Windows refused
  to remove the folder (#1366). A singleton the container disposes with the host's services now ends it too, and its
  `Dispose` runs once.
