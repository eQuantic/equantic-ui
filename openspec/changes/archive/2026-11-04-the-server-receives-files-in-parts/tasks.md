# Tasks

## 1. The vocabulary

- [x] 1.1 `UploadPolicy` and `UploadedFile` in Primitives, declared in `PublicAPI.Unshipped.txt`; `OpenRead` and `Delete` `[ServerOnly]`
- [x] 1.2 Runtime twins of `UploadPolicy`, `UploadedFile` and `FileFilter`, exported from both doors, registered by value where C# compares by value, and `FileFilter` off the list of types that owe none
- [x] 1.3 Prove it: the conformance suite runs a policy, a filter and an uploaded file on both sides, refusals included, and the compiler refuses client code that reads or deletes the content (EQ2010)

## 2. The server

- [x] 2.1 The origin check of the Server Actions moves into `AllowedRequestOrigins`, which the actions and the uploads ask
- [x] 2.2 `UseUploads`, `UploadsBuilder`, `UploadsOptions` bound from `EQuantic:Uploads` and validated at start
- [x] 2.3 The tus endpoint at `/api/_equantic/uploads` through `tusdotnet`: policy enforcement on creation, the owner, the record beside the bytes, the atomic count of unfinished uploads
- [x] 2.4 The cleaner removes every upload past its expiry, finished or not
- [x] 2.5 A Server Action binds an `UploadedFile` from its id, for its owner and once complete, and streams it
- [x] 2.6 Prove it: `UploadsTests` sends real tus requests through the real endpoint and reads back the bytes a real Server Action received and the files left in the folder; each owner, limit and sweep case fails with its rule taken out

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` citing #1214
- [x] 3.2 The wiki's page for uploads is written with slice C (#1215), where a developer first writes the component that sends a file: this slice has no client yet
