- **2026-10-17 · The client boundary's spec says what the build does**: after #944 merged, its spec
  refused a kept-out type named only in a base list, which the build never did and should not, since
  no twin writes the argument: `Home : StatelessComponent, IMark<Ledger>`, `Home : Shell<Ledger>` and
  `LedgerHolder : Holder<Ledger>` build, in the app and from a library, and no module names `Ledger`.
  The spec now lists the base list, a dictionary's key or value type and a string literal among the
  positions written as nothing, and says what #944's last commit refuses: the enumerator's protocol in
  a spread and a LINQ operator, and a call through a variant receiver. A test pins the three headers,
  the ledger entry of #944 no longer claims the refusal, and the wiki's EQ2010 row says all of it in
  its five languages ([#945](https://github.com/eQuantic/equantic-ui/issues/945)). Proposed and
  archived through OpenSpec (`openspec/specs/client-boundary`).
