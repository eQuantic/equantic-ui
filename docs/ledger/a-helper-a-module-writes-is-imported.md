- **2026-11-26 · A class module imports the runtime's helpers where it writes them**: since #1805 eqc wrote
  `Uri.EscapeDataString(x)` as `$eq.uri.escapeData(x)` without registering the `$eq` import, and a class module took that
  import only from the strategies that registered it. A static class whose one runtime call was the escape shipped a
  module with no import: a real site's order pages loaded, then threw "$eq is not defined" once they hydrated. A class
  module now imports `$eq` wherever its emitted text names it, as a record's and a component's already did
  ([#1882](https://github.com/eQuantic/equantic-ui/issues/1882)). Proposed and archived through OpenSpec
  (`openspec/specs/module-imports`).
