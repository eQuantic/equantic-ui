# Security review

The written hardening review #306 asked for, measured on `main` at `2d97110f1` (2026-11-09) and filed
as #1265. It says what was reviewed, what each part was checked against, what holds and what does
not. Every finding is an issue of its own: a Bug under #1174 with its points, or, for the two rated
high, an issue whose details are in a security advisory published with the preview that fixes them,
0.2.0-preview.68. What has been fixed since is in [Since the review](#since-the-review).

The Content-Security-Policy is not reviewed here: it does not exist yet, and #1266 to #1270 build it.
The dependency side is not reviewed again either: #1177, #1178, #1179, #1180 and #1259 moved the
tooling past its advisories, and `RuntimeLockfilesTests` keeps the lockfile Dependabot reads equal to
the one the runtime installs.

## How it was read

The code was read, not run, area by area, against the question a reviewer asks of each: what a
request can reach, what it can make the server write, and what it can make the server spend. Every
finding below was checked again against the code before it was filed. The two rated high were
tracked in private security advisories until 0.2.0-preview.68 carried their fixes, and their details
are the advisories', so this review names them and their area and nothing more (#1319, #1320). The
evidence blocks are held to
the code by `DocsIndexTests`, so a line that moves fails the suite rather than misleading the next
reader.

## Verdicts

| Area | Holds | Findings |
|---|---|---|
| The shell and what it writes | The script slots, text and attribute encoding, metadata | #1319 (high), #1286 (medium), #1288 (medium), #1295 (low) |
| Server Actions | The allow-list, deserialization, the payload cap, the origin check | #1320 (high), #1290 (low), #1291 (low) |
| Uploads | File names never reach a disk path, the owner check, streaming | #1287 (medium), #1293 (low) |
| Server events | Unknown topics are refused, policies run with their schemes | #1292 (low) |
| The hot-reload relay | It only sends, and only `reload` | #1297 (low) |
| File-serving routes | Path traversal is refused on every route | #1296 (low) |
| The GTM installer | The container id is validated, consent defaults to denied when asked for | #1294 (low) |
| The image optimizer | Local files only, size limits, `Vary: Accept` | #1289 (medium) |
| Response headers | Nothing: none of them is written | Framing, `nosniff` and `Referrer-Policy` belong to #1267 |

## The shell and what it writes

**Holds.** The configuration, the page's state and the culture are written by `System.Text.Json`'s
default encoder, which escapes `<`, `&`, `'`, the line terminators and U+2028 and U+2029, so no value
ends its `<script>`. Text is HTML-encoded and attribute values attribute-encoded inside double
quotes. Metadata values are attribute-encoded as well.

```text
src/eQuantic.UI.Web/Dom/HtmlRenderer.cs:244  sb.Append(HttpUtility.HtmlAttributeEncode(value));
```

**#1319, high, fixed in 0.2.0-preview.68.** A defect in how the shell writes its template:
[GHSA-222h-8rr5-gfrx](https://github.com/eQuantic/equantic-ui/security/advisories/GHSA-222h-8rr5-gfrx).
Every version up to 0.2.0-preview.67 is affected, and an app upgrades to 0.2.0-preview.68 or later.

**#1286, medium, fixed in 0.2.0-preview.68 (#1338).** No URL scheme was checked anywhere. A
destination was rewritten only when it was a rooted path, so a `javascript:` link from a `Link`, a
`TextRun` or a Markdown link was written as it came, and the router handed it to the browser. Now a
destination whose scheme an href never carries is written with no href at all.

```text
src/eQuantic.UI.Web/RenderChannels.cs:86  !LinkScheme.Allows(destination) ? null
```

**#1288, medium, fixed in 0.2.0-preview.68 (#1321).** The shell carries the visitor's theme, density,
culture and page state, and was written with no `Cache-Control`, while the JSON copy of the same state
was sent `no-store`. Now both answers of a page's route are sent `no-store`, from one place.

```text
src/eQuantic.UI.Server/UIExtensions.cs:911  context.Response.Headers.CacheControl = "no-store";
```

**#1295, low, fixed in 0.2.0-preview.68 (#1338).** The atomic sheet wrote a style value as it came
inside its `<style>` element, which a string of the `HtmlStyle` escape hatch could end. Now what the
sheet holds can never end the element.

```text
src/eQuantic.UI.Server/Assets/IAsset.cs:80  return $"<style{idAttr}{ShellNonce.Attribute(nonce)}>{StyleElementText.Of(Content)}</style>";
```

## Server Actions

**Holds.** Only public instance methods carrying `[ServerAction]`, on the scanned components, are
registered, and a call resolves by exact id. Arguments deserialize only into the declared types, from
an allow-list of primitives, collections and the scanned assemblies, with no `$type` handling and a
depth of 32. The body is capped, at 1 MB unless the app sets another limit, by `Content-Length` and by
the server's body-size feature for a chunked body. A request whose `Origin` is not the app's own, or whose `Sec-Fetch-Site` is
`cross-site` or `same-site`, is refused before anything else runs, and authorization runs before the
component is built and before its arguments are read.

```text
src/eQuantic.UI.Server/ServerActionRegistry.cs:55  var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
src/eQuantic.UI.Server/ServerActionsMiddleware.cs:450  return _allowedAssemblies.Contains(type.Assembly);
src/eQuantic.UI.Server/ServerActionsOptions.cs:24  public long MaxRequestBodySize { get; set; } = 1_048_576;
src/eQuantic.UI.Server/ServerActionOrigins.cs:35  return request.Headers["Sec-Fetch-Site"].ToString() is not ("cross-site" or "same-site");
```

**#1320, high, fixed in 0.2.0-preview.68.** A defect in how Server Actions are authorized:
[GHSA-f5p8-xvhw-4j43](https://github.com/eQuantic/equantic-ui/security/advisories/GHSA-f5p8-xvhw-4j43).
Every version up to 0.2.0-preview.67 is affected, and an app upgrades to 0.2.0-preview.68 or later.

**#1290, low, fixed in 0.2.0-preview.68 (#1316).** The id was the type's simple name and the method's,
and the registry kept the last one registered under it. Now the id is the component's full name, and
two methods under one id stop the app at start, naming both.

```text
src/eQuantic.UI.Server/ServerActionRegistry.cs:64  var actionId = $"{ComponentIdentity.Of(type)}/{attr.Name ?? method.Name}";
src/eQuantic.UI.Server/ServerActionRegistry.cs:24  if (_actions.TryGetValue(actionId, out var held) && held.Method != descriptor.Method)
```

**#1291, low, fixed in 0.2.0-preview.68 (#1316).** The smaller gaps: a 403 body that names the roles required, an action id logged with
its line breaks, malformed JSON answered 500, an `UploadedFile` inside a DTO that skips its binding, a
cap skipped silently when the body was already read, and a result serialized as its runtime type.

## Uploads

**Holds.** A file name never reaches a disk path: ids come from the tus engine, and the owner check
runs before the store is touched. The size is checked against the declared length at creation, the
bytes stream to disk, the origin is checked, and the SDK never serves an upload back.

**#1287, medium, fixed in 0.2.0-preview.68 (#1326).** A request without the owner cookie is a new owner
every time, the limit on unfinished uploads was counted per owner, and a finished upload never
counted, so nothing bounded what an anonymous client could store. Now finished uploads count, per
owner and across the server, in uploads and in bytes.

```text
src/eQuantic.UI.Server/Uploads/UploadOwner.cs:48  var made = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
src/eQuantic.UI.Server/Uploads/UploadStore.cs:125  if (_total.Uploads + _total.ReservedUploads >= _options.MaxTotalUploads)
```

**#1293, low, fixed in 0.2.0-preview.68 (#1326).** The type was the client's, the name kept `..`, a
folder under the web root was not refused, and the owner cookie was `Secure` only when the request
reached the app over HTTPS. Now it is `Secure` everywhere but plain HTTP to this machine or in
Development.

```text
src/eQuantic.UI.Server/Uploads/UploadOwner.cs:81  Secure = context.Request.IsHttps || !(IsLoopback(context.Request.Host.Host) || IsDevelopment(context)),
```

## Server events

**Holds.** A topic no rule matches is refused, policies are evaluated with their schemes and the topic
as resource, connection ids are 128 random bits, and bodies, topic lengths, topics per connection and
the queue are bounded.

```text
src/eQuantic.UI.Server/ServerEvents/ServerTopicAuthorizer.cs:40  if (ruling.Count == 0) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Unknown);
```

**#1292, low.** A subscription skips the origin check the other endpoints make, authorization runs
once at subscribe, connections have no cap and no endpoint an app can rate-limit, and frames from the
backplane are written without the check the publisher makes.

## The hot-reload relay

**Holds.** It is a server-sent event stream, not SignalR. A client cannot send anything, it only
receives a keep-alive and `reload`, and rebuilds start from the file watcher, never from a request.

**#1297, low.** It was on whenever `DOTNET_WATCH` was set, Production included, and each stream gets an
unbounded channel. Since #1361, Production streams no rebuilds unless the app asks for them:

```text
src/eQuantic.UI.Server/UIOptions.cs:90  HotReload ?? (environment.IsDevelopment() || (dotnetWatch == "1" && !environment.IsProduction()));
src/eQuantic.UI.Server/HotReload/HotReloadService.cs:290  var channel = Channel.CreateUnbounded<string>();
```

## File-serving routes

**Holds.** Every route that maps a request to a file refuses both separators and a rooted name, and
checks the resolved path against its directory. Fonts are looked up in a set built at start, and a
culture's name only among the predefined cultures, so nothing is loaded by a request's name.

```text
src/eQuantic.UI.Server/AssetPaths.cs:21  return file.StartsWith(root, StringComparison.Ordinal) ? file : null;
```

**#1296, low.** The component fallback sets 404 and then serves the file it found, its production 404
echoes the name as JavaScript, and the culture redirect builds its `Location` from the decoded path.

## The GTM installer

**Holds.** The container id is validated at start, the values reach the scripts from configuration
and never from a request, and with consent the container is fetched only after a grant, with Consent
Mode defaulting to denied.

**#1294, low.** A revocation was ignored on the page, one grant granted all four signals, the consent
cookie was not `Secure`, and the strings were quoted by hand, the pattern #526 removed from the shell.
Since #1360 consent follows each answer, a grant gives analytics alone by default, the cookie is kept
where only the page's host can write it, the plain `UseGtm(...)` waits for consent, and every string
is encoded as JSON:

```text
src/eQuantic.UI.Gtm/GtmExtensions.cs:159  private static string Js(string value) => $"\"{JsonEncodedText.Encode(value)}\"";
```

## The image optimizer

**Holds.** It reads local files only, under the web root, with a source limit of 10 MB, a pixel limit
checked before decoding, ten optimizations at once, and `Vary: Accept`.

```text
src/eQuantic.UI.Images/ImageOptimizationMiddleware.cs:92  if (!resolvedPath.StartsWith(webRootFull, StringComparison.OrdinalIgnoreCase))
```

**#1289, medium, fixed in 0.2.0-preview.68 (#1336).** The cache was keyed on the url as the request
spelled it, and on any quality from 1 to 100, with no size cap, so one file cost a decode and a cached
copy per spelling. Now the key is the file itself, as found under the web root, and a quality is
settled to the ones the options allow.

```text
src/eQuantic.UI.Images/ImageOptimizationMiddleware.cs:127  var source = $"{relative}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
src/eQuantic.UI.Images/ImageOptimizationMiddleware.cs:82  quality = options.QualityFor(quality);
```

## Response headers

On 2026-11-09, no `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`,
`frame-ancestors`, `Referrer-Policy` or `Strict-Transport-Security` was written anywhere in `src`, so
every page could be framed by any origin, until an app turns on the policy that
[Since the review](#since-the-review) describes. They are not filed separately: #1267 writes the policy and the headers beside
it. HSTS is ASP.NET Core's own `UseHsts`, the app's call.

## What was not reviewed

How a deployed app orders its middleware (the template and the samples call no `UseAuthorization`),
the chart libraries' own handling of labels, and the native track, whose surfaces are not served over
HTTP.

## Since the review

The findings above are as measured on 2026-11-09. Their fixes, by the release that carries them:

| Finding | Fixed by | Release |
|---|---|---|
| #1319 (high), the shell's template | #1321 | 0.2.0-preview.68 |
| #1320 (high), Server Actions' authorization | #1316 | 0.2.0-preview.68 |
| #1286 (medium), a `javascript:` destination | #1338 | 0.2.0-preview.68 |
| #1287 (medium), anonymous uploads | #1326 | 0.2.0-preview.68 |
| #1288 (medium), the shell's `Cache-Control` | #1321 | 0.2.0-preview.68 |
| #1289 (medium), the image cache's key | #1336 | 0.2.0-preview.68 |
| #1290 and #1291 (low), Server Actions | #1316 | 0.2.0-preview.68 |
| #1293 (low), uploads | #1326 | 0.2.0-preview.68 |
| #1295 (low), the atomic sheet | #1338 | 0.2.0-preview.68 |
| #1294 (low), GTM's consent | #1360 | the next release |
| #1296 and #1297 (low), the routes and hot reload | #1361 | the next release |
| #1292 (low), server events | #1358, open | |

The response headers the review found missing came with the Content-Security-Policy, also in
0.2.0-preview.68: an app turns it on with `UseContentSecurityPolicy` (#1267), every script and style
the SDK writes carries the request's nonce (#1266, #1268), the policy brings `nosniff` and
`Referrer-Policy`, and only the app's own origin may frame a page unless it says otherwise. The build
refuses an escape-hatch inline handler under it (EQ2115, #1269), and a real browser runs the dashboard
under it, failing on a violation (#1270, #1370).
