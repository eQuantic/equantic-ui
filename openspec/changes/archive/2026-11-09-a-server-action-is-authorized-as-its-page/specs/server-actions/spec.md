## ADDED Requirements

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
