- **2026-11-09 · GTM consent follows every answer, and is kept where only the page's host can write it**:
  a plain `UseGtm` now waits for the visitor's consent (`WithoutConsent()` says otherwise); a grant turns
  on `analytics_storage` alone unless the app names more (`WithConsentGrants`); a denial after a grant
  turns every Consent Mode signal back to `denied` on the page; over HTTPS the
  answer is kept in `__Host-eq-consent`, which the browser, the server and the installer read alone there;
  and the installer's scripts write configured names as JSON string literals
  ([#1294](https://github.com/eQuantic/equantic-ui/issues/1294)). Proposed and archived through OpenSpec (`openspec/specs/consent`).
