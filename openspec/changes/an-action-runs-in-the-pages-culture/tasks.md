## 1. The action carries its page's culture

- [x] 1.1 `ServerActionsClient` names the active culture in the `culture` and `ui-culture` query keys. Verify: `server-actions.spec.ts` passes, and fails on main
- [x] 1.2 The app's middleware applies it, on both overloads. Verify: `ServerActionCultureTests` reads Portuguese with `Accept-Language: en`, with a cookie that says `es`, and the next provider's culture when the name is unsupported or absent

## 2. A switch asks for what was written

- [x] 2.1 `CultureBridge` writes `catalogs`, `boot.ts` hands it over and the culture store walks only those. Verify: `culture.spec.ts` and `CultureBridgeTests` pass, and the store's cases fail on main

## 3. Against the real thing

- [x] 3.1 A sample in a browser. Verify: `samples/SharedLibrary` switches to English with no 404 in the network log, and an action called from a `pt-BR` page with an English browser answers in Portuguese

## 4. Documentation

- [x] 4.1 The wiki's Localization page in all five languages
- [x] 4.2 One entry for the ledger citing #580 and #825, as a file of its own under `docs/ledger/`
