# Tasks

## 1. A route's title reaches the document (#416)

- [x] 1.1 Hand each route's declaration to the shell and to the navigation endpoint, and write a document's metadata through one builder: the app's defaults, the route's title and description, then the page's own. Verify: `PageTitleTests` answers the page's, the route's and the app's title and description on a load and on a navigation, and fails against main (6 of 9)
- [x] 1.2 Measure it in a browser. Verify: on the dashboard sample `/charts` serves its declared title and a client navigation to `/clock` ends on its own, where main showed `eQuantic Console` for both

## 2. The client's configuration is JSON (#526)

- [x] 2.1 Serialize `window.__EQ_CONFIG` with System.Text.Json. Verify: `PageTitleTests.TheClientConfiguration_IsJson_AndCarriesEveryStringAsWritten` parses it and reads a line break, a `</script>` and a quoted cookie name back, and the culture and not-found suites read it as JSON

## 3. Documentation and the suites

- [x] 3.1 The wiki's ServerIntegration page (EN + pt-BR) on a branch named like this one. Verify: the wiki guards pass with `EQ_WIKI_DIR` on it
- [x] 3.2 The suites, each alone and read by its exit code. Verify: Server and Web exit 0
