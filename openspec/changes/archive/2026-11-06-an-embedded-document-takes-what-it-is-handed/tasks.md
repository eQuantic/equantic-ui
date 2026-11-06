# Tasks

## 1. The vocabulary

- [x] 1.1 Append `Payment`, `Camera`, `Microphone`, `Location`, `Fullscreen`, `CopyToClipboard` and `Passkeys` to `WebSandbox`, declared in `PublicAPI.Unshipped.txt` and recorded in the enum ABI baseline. Verify `EnumValueAbiTests` passes.

## 2. The two halves of the web target

- [x] 2.1 Write the delegated features into `allow` in the C# realizer, and no `allow` for a frame handed none. Verify `WebFrameRealizerTests`, the checkout's combination included, and that every flag is written in exactly one of the two attributes.
- [x] 2.2 Write the same in `lowering.ts`, and pin both halves against `web-frame-attributes.fixture.json`. Verify `lowering.spec.ts` reads every case of the fixture and `TestRuntime` passes.

## 3. The docs

- [x] 3.1 Document `WebFrame` and its flags on the wiki's Components page in all five languages, and compile the page's snippet in `WikiClaimsCompile`. Verify the web tests pass against the wiki branch.
- [x] 3.2 Write the ledger entry under `docs/ledger/`, citing #1228.
