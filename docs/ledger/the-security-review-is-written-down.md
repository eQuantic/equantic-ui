- **2026-11-09 · The security hardening review is written down**: `docs/SECURITY-REVIEW.md` reviews
  the shell and what it writes, Server Actions, uploads, server events, the hot-reload relay, the
  file-serving routes, the GTM installer, the image optimizer and the response headers on `main` at
  `2d97110f1`, says what holds in each with the line that shows it, and files every finding as a Bug
  under #1174: #1284 to #1297, two of them high. A placeholder inside a value the shell already
  inserted was replaced again, so a visitor's `{{InitialState}}` in an attribute wrote the page's
  state there and its first quote closed the attribute, reproduced on the real template (#1284). A
  Server Action ignored ASP.NET Core's `[Authorize]`, its schemes and the app's fallback policy,
  which its page honors (#1285)
  ([#1265](https://github.com/eQuantic/equantic-ui/issues/1265)).
