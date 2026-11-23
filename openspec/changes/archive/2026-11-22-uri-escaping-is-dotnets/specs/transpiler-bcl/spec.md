## ADDED Requirements

### Requirement: Uri's escaping is .NET's

`Uri.EscapeDataString`, `Uri.EscapeUriString` and `Uri.UnescapeDataString` SHALL answer in the browser what they
answer in .NET: the same characters kept and escaped, a lone surrogate written as U+FFFD, a malformed escape or a
byte that starts no well-formed UTF-8 character left as written, and a null text refused with the
`ArgumentNullException` .NET throws.

#### Scenario: The characters encodeURIComponent keeps

- **WHEN** browser-side code computes `Uri.EscapeDataString("Hello! (cake) it's *the* one")`
- **THEN** it is `Hello%21%20%28cake%29%20it%27s%20%2Athe%2A%20one`, as in .NET

#### Scenario: A link built in a component

- **WHEN** a page links to `"https://wa.me/1?text=" + Uri.EscapeDataString("Hello! I'd like a cake (14 cm) * 2")`
- **THEN** the page hydrates with the link the server drew, and no difference

#### Scenario: A malformed escape

- **WHEN** browser-side code computes `Uri.UnescapeDataString("%C3%28")` and `Uri.UnescapeDataString("100%")`
- **THEN** they are `%C3(` and `100%`, as in .NET, and nothing throws

#### Scenario: The brackets of a URI

- **WHEN** browser-side code computes `Uri.EscapeUriString("http://x/a b?q=[1]")`
- **THEN** it is `http://x/a%20b?q=[1]`, as in .NET
