# Tasks

## 1. The endpoint refuses a request from another site

- [ ] 1.1 The rule: an `Origin` that is not the app's own host or an allowed origin is refused, the
      opaque `null` included; without one, `Sec-Fetch-Site: cross-site` or `same-site` is refused;
      with neither, the request runs
- [ ] 1.2 The app's own host: the request's `Host`, or the first `X-Forwarded-Host`, host and port
      compared with a scheme's default port left out
- [ ] 1.3 A refusal is a 403 with the endpoint's error body, logged at warning level, before the
      body is read
- [ ] 1.4 Check: `eQuantic.UI.Server.Tests`, a test per case of the rule, each asserting whether the
      action ran

## 2. An app allows named origins

- [ ] 2.1 `ServerActionsOptions`, bound from `EQuantic:ServerActions` and validated at start
- [ ] 2.2 `UIOptions.AllowServerActionOrigins`, adding to what configuration holds
- [ ] 2.3 The public surface (`PublicAPI.Unshipped.txt`) and the developer surface baseline
- [ ] 2.4 Check: a start-up test that a malformed origin stops the app, and the dashboard sample's
      actions still running in a browser

## 3. Documentation

- [ ] 3.1 The wiki's Security page, in English and Portuguese
- [ ] 3.2 One `docs/LEDGER.md` line citing #678
