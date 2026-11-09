# Security review

The written hardening review #306 asked for, measured on `main` at `2d97110f1` (2026-11-09) and filed
as #1265. It says what was reviewed, what each part was checked against, what holds and what does
not. Every finding is an issue of its own, a Bug under #1174 with its points.

The Content-Security-Policy is not reviewed here: it does not exist yet, and #1266 to #1270 build it.
The dependency side is not reviewed again either: #1177, #1178, #1179, #1180 and #1259 moved the
tooling past its advisories, and `RuntimeLockfilesTests` keeps the lockfile Dependabot reads equal to
the one the runtime installs.

## How it was read

The code was read, not run, area by area, against the question a reviewer asks of each: what a
request can reach, what it can make the server write, and what it can make the server spend. Every
finding below was checked again against the code before it was filed. The one rated high that
depends on how HTML parses was reproduced on the real shell template, through the engine, with the
values the server writes (#1284). The evidence blocks are held to the code by `DocsIndexTests`, so a
line that moves fails the suite rather than misleading the next reader.

## Verdicts

| Area | Holds | Findings |
|---|---|---|
| The shell and what it writes | The script slots, text and attribute encoding, metadata | #1284 (high), #1286 (medium), #1288 (medium), #1295 (low) |
| Server Actions | The allow-list, deserialization, the payload cap, the origin check | #1285 (high), #1290 (low), #1291 (low) |
| Uploads | File names never reach a disk path, the owner check, streaming | #1287 (medium), #1293 (low) |
| Server events | Unknown topics are refused, policies run with their schemes | #1292 (low) |
| The hot-reload relay | It only sends, and only `reload` | #1297 (low) |
| File-serving routes | Path traversal is refused on every route | #1296 (low) |
| The GTM installer | The container id is validated, consent defaults to denied when asked for | #1294 (low) |
| The image optimizer | Local files only, size limits, `Vary: Accept` | #1289 (medium) |
| Response headers | Nothing is set | Framing, `nosniff` and `Referrer-Policy` belong to #1267 |

## The shell and what it writes

**Holds.** The configuration, the page's state and the culture are written by `System.Text.Json`'s
default encoder, which escapes `<`, `&`, `'`, the line terminators and U+2028 and U+2029, so no value
ends its `<script>`. Text is HTML-encoded and attribute values attribute-encoded inside double
quotes. Metadata values are attribute-encoded as well.

```text
src/eQuantic.UI.Web/Dom/HtmlRenderer.cs:244  sb.Append(HttpUtility.HtmlAttributeEncode(value));
```

**#1284, high.** The template engine replaces each variable over the whole buffer, one after another,
so a placeholder inside a value it already inserted is replaced again by a later variable. The
server-rendered content is inserted before the page's state, so a visitor's `{{InitialState}}` in an
attribute becomes the state's JSON there, and its first quote closes the attribute. Reproduced: the
input `{{InitialState}} onmouseover=alert(1) x` renders an element with a real `onmouseover`.

```text
src/eQuantic.UI.Server/HtmlTemplateEngine.cs:114  builder.Replace(placeholder, kvp.Value ?? string.Empty);
```

**#1286, medium.** No URL scheme is checked anywhere. A destination is rewritten only when it is a
rooted path, so a `javascript:` link from a `Link`, a `TextRun` or a Markdown link is written as it
came, and the router hands it to the browser.

```text
src/eQuantic.UI.Web/Dom/RenderContext.cs:62  public static string ResolveDestination(string destination) =>
```

**#1288, medium.** The shell carries the visitor's theme, density, culture and page state, and is
written with no `Cache-Control`, while the JSON copy of the same state is sent `no-store`.

```text
src/eQuantic.UI.Server/UIExtensions.cs:1091  context.Response.ContentType = "text/html";
src/eQuantic.UI.Server/UIExtensions.cs:699  context.Response.Headers["Cache-Control"] = "no-store";
```

**#1295, low.** The atomic sheet writes a style value as it came inside its `<style>` element, which a
string of the `HtmlStyle` escape hatch can end.

```text
src/eQuantic.UI.Server/Assets/IAsset.cs:78  return $"<style{idAttr}>{Content}</style>";
```

## Server Actions

**Holds.** Only public instance methods carrying `[ServerAction]`, on the scanned components, are
registered, and a call resolves by exact id. Arguments deserialize only into the declared types, from
an allow-list of primitives, collections and the scanned assemblies, with no `$type` handling and a
depth of 32. The body is capped at 1 MB, by `Content-Length` and by the server's body-size feature for
a chunked body. A request whose `Origin` is not the app's own, or whose `Sec-Fetch-Site` is
`cross-site` or `same-site`, is refused before anything else runs, and authorization runs before the
component is built and before its arguments are read.

```text
src/eQuantic.UI.Server/ServerActionRegistry.cs:43  var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
src/eQuantic.UI.Server/ServerActionsMiddleware.cs:383  return _allowedAssemblies.Contains(type.Assembly);
src/eQuantic.UI.Server/ServerActionsMiddleware.cs:44  private const long MaxRequestBodySize = 1_048_576;
src/eQuantic.UI.Server/ServerActionOrigins.cs:35  return request.Headers["Sec-Fetch-Site"].ToString() is not ("cross-site" or "same-site");
```

**#1285, high.** An action is authorized by other rules than its page. The page accepts ASP.NET
Core's own `[Authorize]`, its schemes and the app's fallback policy. The action reads only the SDK's
attribute, checks the default scheme's user, and is public when it names nothing.

```text
src/eQuantic.UI.Server/Authorization/PageAuthorization.cs:47  case IAuthorizeData or IAllowAnonymous:
src/eQuantic.UI.Server/Authorization/ServerActionAuthorizationService.cs:11  using AuthorizeAttribute = eQuantic.UI.Primitives.AuthorizeAttribute;
src/eQuantic.UI.Server/Authorization/ServerActionAuthorizationService.cs:69  // If no authorization attributes, action is public by default
```

**#1290, low.** The id is the type's simple name and the method's, and the registry keeps the last
one registered under it.

```text
src/eQuantic.UI.Server/ServerActionRegistry.cs:49  var actionId = $"{type.Name}/{attr.Name ?? method.Name}";
src/eQuantic.UI.Server/ServerActionRegistry.cs:19  _actions[actionId] = descriptor;
```

**#1291, low.** The smaller gaps: a 403 body that names the roles required, an action id logged with
its line breaks, malformed JSON answered 500, an `UploadedFile` inside a DTO that skips its binding, a
cap skipped silently when the body was already read, and a result serialized as its runtime type.

## Uploads

**Holds.** A file name never reaches a disk path: ids come from the tus engine, and the owner check
runs before the store is touched. The size is checked against the declared length at creation, the
bytes stream to disk, the origin is checked, and the SDK never serves an upload back.

**#1287, medium.** A request without the owner cookie is a new owner every time, the limit on
unfinished uploads is counted per owner, and a finished upload never counts, so nothing bounds what
an anonymous client can store.

```text
src/eQuantic.UI.Server/Uploads/UploadOwner.cs:48  var made = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
src/eQuantic.UI.Server/Uploads/UploadStore.cs:104  var unfinished = _records.Values.Count(record => record.Owner == owner && !_finished.Contains(record.Id));
```

**#1293, low.** The type is the client's, the name keeps `..`, a folder under the web root is not
refused, and the owner cookie is `Secure` only when the request reached the app over HTTPS.

```text
src/eQuantic.UI.Server/Uploads/UploadOwner.cs:76  Secure = context.Request.IsHttps,
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

**#1297, low.** It is on whenever `DOTNET_WATCH` is set, Production included, and each stream gets an
unbounded channel.

```text
src/eQuantic.UI.Server/UIOptions.cs:87  HotReload ?? (environment.IsDevelopment() || dotnetWatch == "1");
src/eQuantic.UI.Server/HotReload/HotReloadService.cs:146  var channel = Channel.CreateUnbounded<string>();
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

**#1294, low.** A revocation is ignored on the page, one grant grants all four signals, the consent
cookie is not `Secure`, and the strings are quoted by hand, the pattern #526 removed from the shell.
Whether the plain `UseGtm(...)` should ask for consent by default is a product decision recorded there.

```text
src/eQuantic.UI.Gtm/GtmExtensions.cs:148  value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("</", "<\\/");
```

## The image optimizer

**Holds.** It reads local files only, under the web root, with a source limit of 10 MB, a pixel limit
checked before decoding, ten optimizations at once, and `Vary: Accept`.

```text
src/eQuantic.UI.Images/ImageOptimizationMiddleware.cs:91  if (!resolvedPath.StartsWith(webRootFull, StringComparison.OrdinalIgnoreCase))
```

**#1289, medium.** The cache is keyed on the url as the request spelled it, and on any quality from 1
to 100, with no size cap, so one file costs a decode and a cached copy per spelling.

```text
src/eQuantic.UI.Images/ImageCache.cs:167  var input = $"{url}|{width}|{quality}|{format}";
```

## Response headers

No `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `frame-ancestors`,
`Referrer-Policy` or `Strict-Transport-Security` is written anywhere in `src`, so every page can be
framed by any origin. They are not filed separately: #1267 writes the policy and the headers beside
it. HSTS is ASP.NET Core's own `UseHsts`, the app's call.

## What was not reviewed

How a deployed app orders its middleware (the template and the samples call no `UseAuthorization`),
the chart libraries' own handling of labels, and the native track, whose surfaces are not served over
HTTP.
