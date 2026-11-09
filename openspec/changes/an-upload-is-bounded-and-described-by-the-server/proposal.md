## Why

Closes #1287 and #1293, Bugs the security review (#1265) found in the uploads.

- **#1287.** The uploads a server kept were bounded only by each owner's unfinished ones. A browser that
  sends no cookie is a new owner on every request and a finished upload counted for nothing, so nothing
  bounded the disk or the records held in memory, and every creation counted the records under the lock.
- **#1293.** An upload's type was what the device said, its name kept what a file system refuses, its
  folder could sit under the web root, and its owner's cookie took `Secure` from the request's scheme.

Measured on main, the 18 tests this change adds all fail there.

## What Changes

- **Quotas**, read from `EQuantic:Uploads`: `MaxUploadsPerOwner` (100) and `MaxBytesPerOwner` (1 GiB) per
  owner, `MaxTotalUploads` (10,000) and `MaxTotalBytes` (10 GiB) for the store, a finished upload counted
  until it expires. A creation past a limit on uploads answers 429, past one on bytes 413, naming the limit.
  The store keeps a tally per owner and a total, so a creation no longer counts every record.
- **The type** is the one the file's extension names, as ASP.NET Core's static files read it; the device's
  `filetype` is never read.
- **The name** loses control characters, has the characters a file system refuses written as `_`, loses
  the dots and spaces Windows drops from its end, is never `.` or `..`, gains a `_` before a name Windows
  keeps for a device, and is at most 255 characters.
- **The folder** under the web root stops the app at start.
- **The cookie** is `Secure` but for plain HTTP to this machine or a private-network address.
- **A policy larger than a byte quota** stops the app at start, naming it: every upload under it would be refused.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `file-uploads`: two new requirements, the quotas and what the server says of a file.

## Impact

- **The server**: `Uploads/` (`UploadsOptions` and its validator, `UploadStore`, `UploadReservation`,
  `UploadProtocol`, `UploadOwner`).
- **The public surface** (`eQuantic.UI.Server`): four `UploadsOptions` properties.
- **The developer surface**: four keys in `EQuantic:Uploads`, a section already bound.
