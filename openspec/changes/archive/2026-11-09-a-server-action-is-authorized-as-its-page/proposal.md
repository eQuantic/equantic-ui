## Why

Closes #1320, a security fix in Server Actions tracked in a private advisory, and #1290 and #1291, Bugs the
security review (#1265) found at the Server Action endpoint.

- **#1320.** A page and its actions are to be authorized by the same rules: the SDK's and ASP.NET Core's
  attributes, their authentication schemes, and the app's fallback policy.
- **#1290.** An action's id was its component's simple name and the method's, and the registry kept the
  last of two under one id without a word.
- **#1291.** The smaller gaps: what a refusal says, what the log receives, how malformed arguments are
  answered, an `UploadedFile` inside a value, the body's cap, the result's type, and the body's content type.

Measured on main, 16 of the 20 tests this change adds fail there; the other four are the cases that
must keep passing.

## What Changes

- **One rule for a page and an action** (`RequestAuthorization`): the SDK's and ASP.NET Core's attributes,
  read as the middleware reads an endpoint's, combined into one policy, the `FallbackPolicy` standing for
  none, the policy's schemes authenticated, and the request as the resource. An action reads its
  component's attributes and then its method's; the closest one wins, so a method's `[Authorize]` closes
  what its component's `[AllowAnonymous]` opens. `AddUI` registers ASP.NET Core's authorization services,
  which the rule needs, and changes nothing an app registered.
- **An action's id is its component's CLR full name** and the action's name, on both sides: eqc reads it
  off the declaration (`ComponentDefinition.TypeIdentity`) and the server off the type
  (`ComponentIdentity.Of`). Two methods under one id stop the app at start, naming both.
- **The endpoint**: a body that is not JSON is refused with 415; every body is read through its cap,
  which `EQuantic:ServerActions:MaxRequestBodySize` sets (1 MB unless the app says otherwise); a 403 says
  "Access denied." and a 404 "Action not found.", the reasons staying in the log; the id is logged without
  its line breaks; malformed arguments answer 400 with a warning; an `UploadedFile` anywhere but an
  action's own parameter is refused; a result is written as the type the action declares.

**Answered with a reason, not changed (#1291):** a 404 against a 401 or a 403 lets a caller tell an
action that exists from one that does not. Every page's module carries the ids of its actions, and the
modules are served to anyone, so the ids are public by construction: what protects an action is its
authorization, which this change makes the page's.

For a developer using the SDK: an action is protected by the attributes they already write, ASP.NET
Core's included, and by their app's fallback policy. A component drawn inside a protected page carries
the page's requirement itself, as the request cannot prove which page drew it. A file an action receives
is a parameter of its own.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `server-actions`: three new requirements, for the authorization, the id and the endpoint.

## Impact

- **The server**: `Authorization/` (`RequestAuthorization`, `PageAuthorization`,
  `ServerActionAuthorizationService` and its result), `ServerActionRegistry`, `ServerActionsMiddleware`,
  `ServerActionsOptions` and its validator, `BoundedReadStream`, `UploadedFileArgumentConverter`, `AddUI`.
- **eqc**: the action id in `ComponentParser`.
- **The public surface** (`eQuantic.UI.Server`): `ServerActionAuthorizationService` takes only its
  logger, `ServerActionAuthorizationResult.PolicyFailed` and `RolesFailed` are gone,
  `ServerActionsOptions.MaxRequestBodySize` is new.
