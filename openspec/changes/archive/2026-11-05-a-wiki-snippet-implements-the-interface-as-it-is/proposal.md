# Proposal

Closes #1226, a Bug under #1035 (Any design system in C#, complete on the web and on native, with no
CSS). Met while explaining `0.2.0-preview.66`'s fonts to an app moving to it.

## Why

Since `0.2.0-preview.66`, `IAppTheme.TextFamily` is a `FontFamily?`, and nothing converts a string to
it. The DesignSystem page's "The document is the theme's" still shows
`public string? TextFamily => "Atkinson Hyperlegible Next";` in all five languages, so a reader who
copies it meets CS0738 and no word of the shape that replaced it.

Nothing failed because `WikiClaimsCompile` compiles the snippets someone transcribed into it, and this
one never was. The section beside it, "Naming a typeface", had been transcribed and moved with the
change. A page's snippet was held only when somebody remembered to copy it.

## What Changes

- The example says `public FontFamily? TextFamily => new("Atkinson Hyperlegible Next");`, in every
  language the page is written in.
- `WikiImplementationsTests` compiles every C# block of every wiki page and language alone, against
  the SDK's assemblies, and reads each type it declares from the compiler's own symbols: a member named
  like a member of an SDK interface the type implements SHALL have that member's signature (its kind,
  its type with its nullability, and a method's parameters). A block is a fragment, so a member it
  leaves out is not an offence, and a type the compilation cannot bind compares by the name the page
  wrote. An interface `src/` declares that the compilation cannot resolve is an offence, not a skip.
- `eQuantic.UI.Web.Tests` references `eQuantic.UI.Server`, whose contracts (`IRequireAssets`,
  `IHandleMetadata`, `IServerEventHandler`, `IComponentAssetProvider<T>`) the wiki's snippets
  implement. Without it 35 of them would have been read by nothing.
- Controls without a wiki: each kind of drift the guard exists for is an offence, and the snippets the
  pages write as they should are not.

Nothing an app writes moves: no public surface, no developer surface.

## Impact

- The wiki's `DesignSystem` page, in English, German, Spanish, French and Portuguese
- `tests/eQuantic.UI.Web.Tests`: the guard, and the project reference
- `openspec/specs/working-agreement`: the requirement that a wiki snippet implements an interface as
  the interface is
- An entry under `docs/ledger/`
