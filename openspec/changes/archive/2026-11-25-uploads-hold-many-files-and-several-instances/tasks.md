# Tasks

## 1. The storage

- [x] 1.1 `IUploadStorage`, `StoredUpload` and `UploadsBuilder.UseStorage<TStorage>()` in `eQuantic.UI.Server`, declared in `PublicAPI.Unshipped.txt`
- [x] 1.2 The folder storage as the default, appending only at its length, with the records read back after a restart
- [x] 1.3 The engine's store as an adapter over any storage; the endpoint offers creation, termination and expiration
- [x] 1.4 The store orchestrates over the storage: the ids it gives alone, the limits counted from the storage with the places held under one lock, the expiry swept from the storage
- [x] 1.5 The unfinished limit's 429 carries `Retry-After: 5`
- [x] 1.6 Prove it: `UploadsTests` and `UploadsLimitsTests` hold over the default; two instances over one storage serve one upload and share the limits; a foreign id never reaches the storage; each fails with the most plausible wrong fix (records cached per instance, the ids passed through)

## 2. The page

- [x] 2.1 `Upload` waits for one of three page-wide slots before its first request, and gives it back when it completes, fails, is paused or is cancelled
- [x] 2.2 A creation answered 429 with `Retry-After` waits and is tried again, for up to ten minutes
- [x] 2.3 A page's first creation goes alone, until the server answers it
- [x] 2.4 Prove it: the runtime's tests drive five uploads, a first creation and a 429 that frees itself through a fake endpoint, each failing with its fix taken out; and the sample's page, in a browser with no upload cookie, sends seven files of two parts each to a server that allows two unfinished uploads, and all seven arrive

## 3. Documentation

- [x] 3.1 The wiki's Uploads page in its five languages: the storage, several instances, what a storage must guarantee, and three files at a time
- [x] 3.2 One ledger entry under `docs/ledger/` citing #1885 and #1886
