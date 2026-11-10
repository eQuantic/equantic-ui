# server-actions Specification

## Purpose
What the SDK's Server Action endpoint accepts: a call the app's own pages make, and none another
site makes for them.

## Requirements

### Requirement: A Server Action refuses a request from another site

The Server Action endpoint SHALL refuse with 403, before the action runs, a request whose `Origin`
is neither the app's own host nor an origin the app allows, the opaque `null` included, and a request
without an `Origin` whose `Sec-Fetch-Site` is `cross-site` or `same-site`. A request with neither
header SHALL run. The app's own host SHALL be the request's `Host`, compared by host and port, a `Host` without a
port standing for the origin scheme's default one, and a raw `X-Forwarded-Host` SHALL NOT be read.

#### Scenario: The app's own page

- **WHEN** an action is posted with `Origin: https://app.example` to the host `app.example`
- **THEN** it runs

#### Scenario: The app's own page at an IPv6 address

- **WHEN** an action is posted with `Origin: https://[::1]:8443` to the host `[::1]:8443`
- **THEN** it runs

#### Scenario: The same host on another port

- **WHEN** an action is posted with `Origin: https://app.example` to the host `app.example:80`
- **THEN** the answer is 403 and the action does not run

#### Scenario: Another site

- **WHEN** an action is posted with `Origin: https://evil.example` to the host `app.example`
- **THEN** the answer is 403 and the action does not run

#### Scenario: An opaque origin

- **WHEN** an action is posted with `Origin: null`
- **THEN** the answer is 403 and the action does not run

#### Scenario: No origin, and the browser says cross-site

- **WHEN** an action is posted with no `Origin` and `Sec-Fetch-Site: cross-site`
- **THEN** the answer is 403 and the action does not run

#### Scenario: Not a browser

- **WHEN** an action is posted with neither `Origin` nor `Sec-Fetch-Site`
- **THEN** it runs

#### Scenario: A forwarded host the request writes itself

- **WHEN** an action is posted with `Origin: https://evil.example` and `X-Forwarded-Host: evil.example` to the host `app.example`
- **THEN** the answer is 403 and the action does not run

### Requirement: An app allows the origins its pages are served from

An app SHALL allow named origins in `EQuantic:ServerActions:AllowedOrigins` and with
`UIOptions.AllowServerActionOrigins`, and an action posted from an allowed origin SHALL run. An
allowed origin that is not an absolute `http` or `https` URI with a host and nothing else SHALL stop
the app at start, naming the value.

#### Scenario: An allowed origin

- **WHEN** `https://admin.example` is allowed and an action is posted with that `Origin` to the host `api.example`
- **THEN** it runs

#### Scenario: A malformed origin

- **WHEN** `https://admin.example/login` is allowed
- **THEN** the app does not start, and the error names `https://admin.example/login`

### Requirement: A Server Action is known by its symbol

A component's method SHALL be a Server Action when its symbol carries `[ServerAction]`, wherever a model
holds its tree, whichever spelling C# accepted: its body SHALL never cross, and its twin SHALL hold the stub
written from its signature. A partial method SHALL be one action.

#### Scenario: Marked through an alias

- **WHEN** `using Rpc = eQuantic.UI.Primitives.ServerActionAttribute;` marks a component's method `[Rpc]`, and
  its body reads a file
- **THEN** the build succeeds, the twin invokes `Home/Archived`, and nothing of the body crosses

#### Scenario: Named through an alias

- **WHEN** the same method is marked `[Rpc(Name = "Stored")]`
- **THEN** the twin invokes `Home/Stored`

#### Scenario: The defining half of a partial method

- **WHEN** `[ServerAction]` is written on the defining half of a partial method, and the body on the other
- **THEN** the twin holds one stub and nothing of the body

### Requirement: A Server Action runs in the culture of its page

The runtime SHALL name the page's format culture and language on every Server Action call, in the
`culture` and `ui-culture` query keys, so the app's request localization applies them ahead of the cookie
and `Accept-Language`. A culture the app does not support SHALL be refused there, and the next provider
SHALL decide. A page with no culture installed SHALL name none.

#### Scenario: A Portuguese page with an English browser

- **WHEN** a page in `pt-BR` calls an action, with `Accept-Language: en` and no culture cookie
- **THEN** the handler runs in `pt-BR`, and `1234.5.ToString("N1")` answers `1.234,5`

#### Scenario: A cookie that says otherwise

- **WHEN** a page in `pt-BR` calls an action with a culture cookie that says `es`
- **THEN** the handler runs in `pt-BR`

#### Scenario: A switch with no reload

- **WHEN** a page switches from `pt-BR` to `es` and calls an action
- **THEN** the call names `es`

#### Scenario: A culture the app does not support

- **WHEN** a call names `de`, which the app does not support, with `Accept-Language: pt-BR`
- **THEN** the handler runs in `pt-BR`

#### Scenario: A call that names no culture

- **WHEN** a call names no culture
- **THEN** the cookie and `Accept-Language` decide, as they do for any request

### Requirement: A Server Action is authorized by the rule a page is

A Server Action SHALL be authorized by the rule a page is: the SDK's `[Authorize]` and `[AllowAnonymous]`
and ASP.NET Core's alike, every one a member carries, combined into one policy, the app's
`FallbackPolicy` standing for none, the policy's authentication schemes authenticated, and the request
as the policy's resource. An action SHALL read its component's attributes and then its method's, and a
method's `[Authorize]` SHALL set aside its component's `[AllowAnonymous]`. A caller the policy does not
authenticate SHALL be answered 401, one it does not authorize 403, and the action SHALL NOT run.

#### Scenario: ASP.NET Core's attribute on the page

- **WHEN** a component marked with ASP.NET Core's `[Authorize(Roles = "Admin")]` has an action, and it is called anonymously
- **THEN** the answer is 401 and the action does not run

#### Scenario: The page's scheme

- **WHEN** a component requires the `Staff` scheme, and its action is called by a user the default scheme signed in
- **THEN** the answer is 401

#### Scenario: The fallback policy

- **WHEN** the app's fallback policy requires a signed-in user, and an action that names no requirement is called anonymously
- **THEN** the answer is 401, and an action marked `[AllowAnonymous]` still runs

### Requirement: A Server Action's id is unique by construction

A Server Action's id SHALL be its component's CLR full name and the action's name, written by eqc and
registered by the server from the same declaration. Two methods registered under one id SHALL stop the app
at start, naming both.

#### Scenario: Two components of one name

- **WHEN** two components named `Invoices`, in two types or two scanned assemblies, each have an `Export` action
- **THEN** each action has an id of its own, and a call runs the one it names

#### Scenario: Two methods under one id

- **WHEN** two methods of one component register under one id
- **THEN** the app stops at start, naming both methods

### Requirement: The Server Action endpoint keeps to its contract

The endpoint SHALL refuse with 415 a body that is not JSON, SHALL read every body through the cap
`EQuantic:ServerActions:MaxRequestBodySize` sets and refuse one past it with 413, SHALL answer a refusal
without the requirements it failed or the id it was given, SHALL log the caller's id without its line
breaks, SHALL answer malformed arguments with 400, SHALL refuse an `UploadedFile` anywhere but an action's
own parameter, and SHALL write a result as the type the action declares.

#### Scenario: A body that is not JSON

- **WHEN** an action is posted as `text/plain`
- **THEN** the answer is 415 and the action does not run

#### Scenario: A result declared as its base

- **WHEN** an action declared to return `Summary` returns a `Detail` with a `Secret`
- **THEN** the response carries the summary's fields and not the secret
