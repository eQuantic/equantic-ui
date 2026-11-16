- **2026-11-11 · A Paste an app binds to a key of its own pastes on the web**: a Bug under #601
  ([#1028](https://github.com/eQuantic/equantic-ui/issues/1028)). The web reads the clipboard only later,
  in the browser's paste event or through `navigator.clipboard.readText()`, so a Paste bound to any key the
  browser does not paste on found nothing to read. `ITextClipboard` reads asynchronously now, as Flutter's
  `Clipboard.getData` does (`ReadAsync` replaces `Read`), the code editor, the sheet and Photon's text field
  paste what arrives, and on the web a paste key waits for the paste event while any other key reads through
  the Clipboard API.
