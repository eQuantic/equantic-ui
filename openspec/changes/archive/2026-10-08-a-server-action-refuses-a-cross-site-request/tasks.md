# Tasks

## 1. The endpoint refuses a request from another site

- [x] 1.1 The rule: an `Origin` that is not the app's own host or an allowed origin is refused, the
      opaque `null` included; without one, `Sec-Fetch-Site: cross-site` or `same-site` is refused;
      with neither, the request runs
- [x] 1.2 The app's own host: the request's `Host`, host and port compared with a scheme's default
      port left out, and a raw `X-Forwarded-Host` never read
- [x] 1.3 A refusal is a 403 with the endpoint's error body, logged at warning level, before the
      body is read
- [x] 1.4 Check: `eQuantic.UI.Server.Tests`, a test per case of the rule, each asserting whether the
      action ran

## 2. An app allows named origins

- [x] 2.1 `ServerActionsOptions`, bound from `EQuantic:ServerActions` and validated at start
- [x] 2.2 `UIOptions.AllowServerActionOrigins`, adding to what configuration holds
- [x] 2.3 The public surface (`PublicAPI.Unshipped.txt`) and the developer surface baseline
- [x] 2.4 Check: a start-up test that a malformed origin stops the app, and the dashboard sample's
      actions still running in a browser

## 3. Documentation

- [x] 3.1 The wiki's Security page, in English and Portuguese
- [x] 3.2 One `docs/LEDGER.md` line citing #678
