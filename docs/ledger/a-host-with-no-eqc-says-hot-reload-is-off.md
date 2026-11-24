- **2026-11-23 · A host with no eqc says hot reload is off**: a server whose project is not built with eQuantic.UI.Sdk,
  as a host serving the pages of a library it references is, says once that hot reload is off for it and asks MSBuild
  no more, where its log opened with MSBuild's MSB4057 and "Build failed" and every save asked again and sent the page
  an error ([#1757](https://github.com/eQuantic/equantic-ui/issues/1757)). Proposed and archived through OpenSpec
  (`openspec/specs/hot-reload`).
