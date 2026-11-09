- **2026-11-09 · The SDK's styles stay inside a strict policy**: the policy writes `style-src` by the
  request's nonce and `style-src-attr 'unsafe-inline'`. The shell's stylesheet links carry the nonce
  as its style elements do, and the runtime stamps the style elements it creates (`eq-atomic`,
  `eq-faces`) before they enter the document. Measured in Chromium 152, an unstamped one is refused
  and gets no sheet. Style attributes are admitted by their own directive: the dashboard's pages
  carry 4 to 25 each (a pressable's pressed fill, a loop's endpoints, an editor's caret), per element
  and often per frame, and server-rendered markup is styled before any script could write them
  through the CSSOM. An injected style element is still refused, and the runtime's one
  `setAttribute('style')` writes through `style.cssText`
  ([#1268](https://github.com/eQuantic/equantic-ui/issues/1268)). Proposed and archived through
  OpenSpec, into `content-security`.
