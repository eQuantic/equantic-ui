- **2026-11-09 · The security hardening review is written down**: `docs/SECURITY-REVIEW.md` reviews
  the shell and what it writes, Server Actions, uploads, server events, the hot-reload relay, the
  file-serving routes, the GTM installer, the image optimizer and the response headers on `main` at
  `2d97110f1`, says what holds in each with the line that shows it, and files every finding as an
  issue. Twelve are Bugs under #1174, #1286 to #1297. The two rated high, one in the shell and one in
  Server Actions, are tracked in private security advisories until the preview that fixes them is
  released (#1319, #1320)
  ([#1265](https://github.com/eQuantic/equantic-ui/issues/1265)).
