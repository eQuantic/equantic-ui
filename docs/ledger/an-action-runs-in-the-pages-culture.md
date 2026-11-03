- **2026-10-30 · A Server Action runs in the culture of its page, and a switch asks for no catalog the build
  did not write**: the actions endpoint has no culture segment, so a Portuguese page with an English
  browser got its handler's answer in English
  ([#580](https://github.com/eQuantic/equantic-ui/issues/580)). Every call now names the page's culture in
  the query keys ASP.NET's own `QueryStringRequestCultureProvider` reads ahead of the cookie, so the app's
  `UseRequestLocalization` applies and checks it, with no transport of the SDK's. Switching to an app's
  neutral language asked for `en.json`, a 404, on the way to `neutral.json`
  ([#825](https://github.com/eQuantic/equantic-ui/issues/825)): the page now names the catalogs the build
  wrote, and the walk asks only for those. Proposed and archived through OpenSpec
  (`openspec/specs/server-actions`, `openspec/specs/page-culture`).
