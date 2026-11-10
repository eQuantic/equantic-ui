# Tasks

## 1. The contract

- [x] 1.1 `IAppIcon.Build(ComponentContext, AppIconUse)` and the `AppIconUse` enum, recorded in the enum ABI baseline and the runtime's generated unions. Verify every implementation in the tree takes the new parameter.

## 2. The icon tool

- [x] 2.1 Compile the icon with the app's usings and its vector catalog, dropping the usings the vocabulary cannot resolve. Verify `IconCompilationTests`, and that both SDKs hand `--using` per `Using` item (`EveryProgramTheSdksStartTests` pins a plain, a static and an aliased one).
- [x] 2.2 Draw vectors with `PortableIconRasterizer` on every machine, and the small cut on a transparent `Backdrop`. Verify `PortableIconRasterizerTests` and that a `Drawing` is drawn from its artwork in `AppIconTests`.
- [x] 2.3 Write `favicon.ico` with 16, 32 and 48 from the small cut, and weigh the downscale by alpha. Verify `FaviconTests` (Native.Engine), the alpha test failing on main's filter.

## 3. The server

- [x] 3.1 Link `/favicon.ico` from every page and answer it at the root, an app's own file first. Verify `FaviconTests` (Server).

## 4. The proof

- [x] 4.1 The dashboard sample declares an icon drawing two SVGs; in a browser its page links the favicon, `/favicon.ico` serves the built file and the 48 is transparent over the page. The desktop sample compiles an icon written with `Box` and `Icon`, drawn into its `.icns`.

## 5. The docs

- [x] 5.1 The wiki's Assets, BuildFlow and Architecture pages in all five languages, `CLAUDE.md`'s bundle list, and the ledger entry citing #1240.
