## MODIFIED Requirements

### Requirement: The server refuses what a policy does not accept

A creation SHALL name its policy, its file and its type in its metadata (`policy`, `filename`, `filetype`) and state its size. The server SHALL refuse it, with a short reason in English as the body, and keep nothing of it: 400 for a policy it does not accept or none named and for a file not named, 401 for a visitor under a policy that requires a signed-in person, 415 for a file whose name ends in none of the policy's extensions (compared without case; a policy without filters accepts any file), 413 for a size past the policy's largest, and 429 when the owner already has the unfinished uploads `EQuantic:Uploads:MaxUnfinished` allows, with `Retry-After: 5`, since that limit frees itself as the owner's uploads finish. A deferred length and a concatenation SHALL NOT be accepted.

#### Scenario: Too large

- **WHEN** a browser creates an upload of 11 bytes under a policy whose largest size is 10
- **THEN** the answer is 413, its body reads "The file is larger than the 10 bytes accepted here.", and the folder holds no file

#### Scenario: Not one of the formats

- **WHEN** a browser creates an upload of `setup.exe` under a policy of `stl`, `obj` and `3mf`
- **THEN** the answer is 415 and its body reads "Only .stl, .obj, .3mf files are accepted here."

#### Scenario: Unfinished uploads at once

- **WHEN** `MaxUnfinished` is 3 and one signed-in person sends twelve creations at once
- **THEN** exactly three are created and nine are answered 429, each with `Retry-After: 5`

### Requirement: An upload is its owner's alone

The owner of an upload SHALL be the signed-in person, by `ClaimTypes.NameIdentifier` or else `sub`, or else the browser, by a random id the server writes into the `eq-uploads` cookie (HttpOnly, SameSite=Lax, Secure over HTTPS, path `/`, with the uploads' expiry as its lifetime, written again by each upload the browser starts). A request SHALL speak for the owner as the signed-in person or as the browser whose cookie started the upload, so a visitor who signs in goes on with it in the same browser. Only the owner SHALL continue an upload, ask for its offset, delete it or receive it in a Server Action, and another request SHALL be answered 403 without changing it. The server SHALL keep each upload's record in its storage beside its bytes, so the same holds after a restart and on every instance that shares the storage.

#### Scenario: Another browser

- **WHEN** a browser has sent 60 of an upload's 100 bytes, and another browser sends HEAD, PATCH and DELETE to it
- **THEN** each is answered 403, and the owner's HEAD still answers 60

#### Scenario: After a restart

- **WHEN** a server is stopped with an upload half sent, and a new server starts over the same folder
- **THEN** a stranger's HEAD is answered 403, and the owner's cookie continues the upload and receives it whole

#### Scenario: A visitor signs in halfway

- **WHEN** a visitor sends 60 of 100 bytes and then, signed in, the same browser sends the rest and calls the action
- **THEN** the upload continues and the action receives it whole, while the same person signed in on another browser is answered 403

### Requirement: The upload folder is a staging area

The uploads SHALL be kept by the app's `IUploadStorage`, by default one that keeps them in the folder `EQuantic:Uploads:Folder` names (a relative one under the content root; by default one of the app's own under the system's temporary folder, logged at start). `EQuantic:Uploads:Expiry` SHALL say how long an upload is kept from the moment it starts, and the server SHALL remove every upload past its expiry, finished or not, its bytes and its record, from whatever storage keeps it, when it starts and every ten minutes after.

#### Scenario: Expired, finished or not

- **WHEN** a finished and an unfinished upload are a day and ten minutes old
- **THEN** the folder holds neither's bytes nor record, a HEAD to the unfinished one is answered 404, and binding the finished one is answered 404

### Requirement: A page sends a file in resumable parts

`IUploads.Start(file, policy)` SHALL answer at once with an `Upload` that sends the `PickedFile` to the upload endpoint under the policy, in parts of at most 8 MB, and SHALL report its `State`, its `Sent` bytes, counting the part in flight, and its `Size`. The uploads one page starts SHALL send at most three files at a time: the others SHALL stay `Waiting`, and SHALL start in the order they were started as one of the three completes, fails, is paused or is cancelled. Until the server first answers one of them, they SHALL go one at a time, so the cookie that names a visitor's browser is written once, by the first. Once the last byte arrived its `State` SHALL be `Completed` and its `Result` an `UploadedFile` whose id is the last segment of the upload's address, and the upload SHALL forget the address it remembered. `Subscribe` SHALL call its listener on every change from then on and stop when disposed.

#### Scenario: Three parts

- **WHEN** a page starts a file of 20 bytes under a policy that accepts it, with parts of 8 bytes
- **THEN** the browser creates the upload with `Upload-Length: 20` and the policy, the file's name and its type in `Upload-Metadata`, sends parts at the offsets 0, 8 and 16, and the upload completes with an `UploadedFile` of size 20 under the policy's name

#### Scenario: More files than the page sends at once

- **WHEN** a page starts five files at once
- **THEN** no creation arrives while three uploads are unfinished, the creations arrive in the order the files were started, and all five complete

#### Scenario: A visitor's first files

- **WHEN** a visitor with no upload cookie drops seven files on a page
- **THEN** the second creation is sent only once the first is answered, and all seven complete for the same browser

### Requirement: An interrupted part is sent again from where the server stands

A part that is not answered, or answered with a server error, an offset conflict (409) or a lock (423), SHALL be sent again after a pause of 0, 1, 3, 5 and then 10 seconds, each try asking the server's offset first, and past the last pause the upload SHALL fail with "The connection to the server was lost." A creation answered 429 with `Retry-After` SHALL stay `Waiting` and be tried again after that many seconds, for up to ten minutes, and then fail with the server's reason. Any other refusal SHALL fail the upload at once with the server's reason as its `Error`.

#### Scenario: A part that failed

- **WHEN** the first part gets no answer and the next is answered 503
- **THEN** the browser asks the offset before each new try, and the upload completes with the server holding every byte once

#### Scenario: A limit that frees itself

- **WHEN** the creation is answered 429 with `Retry-After: 5` and "At most 3 uploads may be unfinished at a time.", and the next creation is answered 201
- **THEN** the upload is `Waiting` in between, creates its upload five seconds later, and completes

#### Scenario: The server refuses

- **WHEN** the creation is answered 429 with "At most 100 uploads may be kept at a time." and no `Retry-After`
- **THEN** the upload has failed with that reason and nothing more was sent

## ADDED Requirements

### Requirement: An app chooses where its uploads are kept

An app SHALL choose where its uploads are kept with `UseUploads(uploads => uploads.UseStorage<TStorage>())`, `TStorage` an `IUploadStorage` registered as a singleton, and without it the server SHALL keep them in the folder. Every request of an upload (its creation, its parts, its offset, its deletion) and the Server Action that receives it SHALL be served from the storage, so several instances over one storage serve one upload whichever instance each request reaches, and the limits SHALL be counted from it. The server SHALL hand the storage only the ids it gives, and SHALL answer any other id as an upload that does not exist without asking the storage. No type of the seam SHALL name the protocol or its library.

#### Scenario: Two instances over one storage

- **WHEN** two instances of an app use one storage, a browser creates an upload on the first, sends its parts to the second, asks its offset on the first, and a Server Action on the second receives it
- **THEN** every request is answered as on one instance, and the action reads the exact bytes sent

#### Scenario: The limits across instances

- **WHEN** two instances use one storage, an owner may keep two uploads and has created one on each
- **THEN** a third creation on either instance is answered 429

#### Scenario: An id the server never gave

- **WHEN** a request names the upload `..%2Fsecret` or any id that is not 32 lowercase hexadecimal digits
- **THEN** it is answered 404, and the storage is never asked for it

### Requirement: A storage appends only where the content stands

`IUploadStorage.AppendAsync(id, offset, content)` SHALL add the content only when `offset` is the length the storage holds for the upload, and SHALL otherwise keep nothing and answer 0. The default storage SHALL hold to it.

#### Scenario: A part for an offset already passed

- **WHEN** the default storage holds 10 bytes of an upload and is asked to append 5 bytes at the offset 5
- **THEN** it answers 0 and still holds the same 10 bytes
