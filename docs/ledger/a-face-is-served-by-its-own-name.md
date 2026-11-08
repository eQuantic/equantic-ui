- **2026-10-31 · A face is served by its own name**: a Bug ([#1080](https://github.com/eQuantic/equantic-ui/issues/1080)).
  Every instance of an app read which faces it could serve from its deployment, the assemblies the host lists and
  a walk through their references, and a single-file app lists none of the assemblies its bundle carries, so a
  family that named its library only as text was declared on no page. A face outside the theme's stylesheet is
  served at its identity with the hash of its bytes beside it, which any instance answers by reading the file, and
  the set read from the deployment is gone. Proved with an app published single-file. Proposed and archived
  through OpenSpec (`font-faces`).
