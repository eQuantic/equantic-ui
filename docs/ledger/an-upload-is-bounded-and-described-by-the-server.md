- **2026-11-09 · An upload is bounded, and described by the server**: Bugs from the security review
  (#1265), [#1287](https://github.com/eQuantic/equantic-ui/issues/1287) and
  [#1293](https://github.com/eQuantic/equantic-ui/issues/1293). The uploads a server keeps are bounded per
  owner and for the whole store, a finished one counted until it expires (`EQuantic:Uploads`), and the store
  keeps tallies instead of counting every record on each creation. An upload's type is the one its
  extension names, its name is one a file system takes as it is, a folder under the web root stops the app,
  and the owner's cookie is `Secure` behind a proxy that ends TLS. Measured on main, the 18 new tests all
  fail there. Proposed and archived through OpenSpec (`file-uploads`).
