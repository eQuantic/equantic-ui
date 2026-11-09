- **2026-11-09 · GTM consent follows every answer, and is kept where only the page's host can write it**:
  a denial after a grant now turns every Consent Mode signal back to `denied` on the page; an app names
  the signals a grant turns on (`WithConsent(GtmConsentSignals…)`, all four by default); over HTTPS the
  answer is kept in `__Host-eq-consent`, which the browser, the server and the installer read alone there;
  and the installer's scripts write configured names as JSON string literals
  ([#1294](https://github.com/eQuantic/equantic-ui/issues/1294)). Gating a plain `UseGtm` by default is
  left to the owner. Proposed and archived through OpenSpec (`openspec/specs/consent`).
