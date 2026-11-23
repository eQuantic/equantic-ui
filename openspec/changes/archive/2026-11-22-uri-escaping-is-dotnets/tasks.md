# Tasks

## 1. The escaping

- [x] 1.1 `utils/uri-escaping.ts`: `escapeData` and `escapeUri` over the text's UTF-8 bytes, each kept or written in upper-case hex, and `unescapeData` decoding each well-formed UTF-8 character of a run of escapes and leaving every other escape as written; `$eq.uri`
- [x] 1.2 `WebPrimitiveStrategy` lowers `EscapeDataString`, `EscapeUriString` and `UnescapeDataString` to them, and matches no `UnescapeUriString`
- [x] 1.3 Verify: `UriEscapingConformanceTests` runs every printable ASCII character, UTF-8, lone surrogates, malformed and well-formed escapes, characters past the BMP and a null text on both sides (33 cases, 25 of them failing against the old lowering); `encodeURIComponent` with the five characters replaced fails the lone surrogates, and `decodeURIComponent` that answers its input when it throws fails the malformed runs; the fixture site's share page hydrates with no difference, and with the old lowering fails naming the link's two texts
