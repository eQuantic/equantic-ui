## ADDED Requirements

### Requirement: An app accepts uploads under the policies it declares

An app SHALL accept uploads by calling `UseUploads` on `AddUI` with the `UploadPolicy` values it declares in C#, and `MapUI` SHALL then map the upload endpoint at `/api/_equantic/uploads`, which speaks the tus 1.0 protocol. No public type of the SDK SHALL name the protocol or its library. Two different policies of one name SHALL be refused when the app configures them.

#### Scenario: A file sent in parts reaches the server

- **WHEN** an app accepts a policy named `models` and a browser creates an upload of 2 500 bytes under it, then sends three parts at their offsets
- **THEN** each part is answered 204, and a HEAD answers an offset of 2 500

#### Scenario: Two policies of one name

- **WHEN** an app accepts a policy named `models` and then another policy named `models`
- **THEN** `AddUI` throws an `ArgumentException` naming `models`

### Requirement: The server refuses what a policy does not accept

A creation SHALL name its policy, its file and its type in its metadata (`policy`, `filename`, `filetype`) and state its size. The server SHALL refuse it, with a short reason in English as the body, and keep nothing of it: 400 for a policy it does not accept or none named and for a file not named, 401 for a visitor under a policy that requires a signed-in person, 415 for a file whose name ends in none of the policy's extensions (compared without case; a policy without filters accepts any file), 413 for a size past the policy's largest, and 429 when the owner already has the unfinished uploads `EQuantic:Uploads:MaxUnfinished` allows. A deferred length and a concatenation SHALL NOT be accepted.

#### Scenario: Too large

- **WHEN** a browser creates an upload of 11 bytes under a policy whose largest size is 10
- **THEN** the answer is 413, its body reads "The file is larger than the 10 bytes accepted here.", and the folder holds no file

#### Scenario: Not one of the formats

- **WHEN** a browser creates an upload of `setup.exe` under a policy of `stl`, `obj` and `3mf`
- **THEN** the answer is 415 and its body reads "Only .stl, .obj, .3mf files are accepted here."

#### Scenario: Unfinished uploads at once

- **WHEN** `MaxUnfinished` is 3 and one signed-in person sends twelve creations at once
- **THEN** exactly three are created and nine are answered 429

### Requirement: An upload is its owner's alone

The owner of an upload SHALL be the signed-in person, by `ClaimTypes.NameIdentifier` or else `sub`, or else the browser, by a random id the server writes into the `eq-uploads` cookie (HttpOnly, SameSite=Lax, Secure over HTTPS, path `/`, with the uploads' expiry as its lifetime, written again by each upload the browser starts). A request SHALL speak for the owner as the signed-in person or as the browser whose cookie started the upload, so a visitor who signs in goes on with it in the same browser. Only the owner SHALL continue an upload, ask for its offset, delete it or receive it in a Server Action, and another request SHALL be answered 403 without changing it. The server SHALL keep each upload's record beside its bytes, so the same holds after a restart.

#### Scenario: Another browser

- **WHEN** a browser has sent 60 of an upload's 100 bytes, and another browser sends HEAD, PATCH and DELETE to it
- **THEN** each is answered 403, and the owner's HEAD still answers 60

#### Scenario: After a restart

- **WHEN** a server is stopped with an upload half sent, and a new server starts over the same folder
- **THEN** a stranger's HEAD is answered 403, and the owner's cookie continues the upload and receives it whole

#### Scenario: A visitor signs in halfway

- **WHEN** a visitor sends 60 of 100 bytes and then, signed in, the same browser sends the rest and calls the action
- **THEN** the upload continues and the action receives it whole, while the same person signed in on another browser is answered 403

### Requirement: A Server Action receives an uploaded file

A Server Action parameter of type `UploadedFile` SHALL be bound from the upload's `id` in the page's argument, and every other member SHALL be the server's record of it. The server SHALL bind it only for the request's owner and only once its last byte has arrived, and otherwise SHALL answer with the actions' failure shape before the action runs: 403 for another owner, 404 for an id it never gave or an upload past its expiry, 409 for an unfinished upload. `OpenRead` SHALL stream the content from where the server keeps it, and `Delete` SHALL remove the upload's bytes and record. On a value the page built, both SHALL throw `InvalidOperationException`, and client code calling either SHALL be refused at the build (EQ2010).

#### Scenario: The bytes, as uploaded

- **WHEN** a Server Action takes `UploadedFile model` and reads `await model.OpenRead()` whole, and the page passes the upload's id with a forged name and size beside it
- **THEN** the action reads the exact bytes sent, and the name, type, size and policy the upload was created with

#### Scenario: Not finished

- **WHEN** a page passes an upload whose 20 bytes have arrived 10
- **THEN** the answer is 409, "The upload is not finished yet.", and the action does not run

### Requirement: The upload folder is a staging area

`EQuantic:Uploads:Folder` SHALL name the folder the uploads are kept in (a relative one under the content root; by default one of the app's own under the system's temporary folder, logged at start), and `EQuantic:Uploads:Expiry` how long an upload is kept from the moment it starts. The server SHALL remove every upload past its expiry, finished or not, its bytes and its record, when it starts and every ten minutes after.

#### Scenario: Expired, finished or not

- **WHEN** a finished and an unfinished upload are a day and ten minutes old
- **THEN** the folder holds neither's bytes nor record, a HEAD to the unfinished one is answered 404, and binding the finished one is answered 404

### Requirement: Another site's page cannot upload

The upload endpoint SHALL refuse with 403 a request whose `Origin`, or whose `Sec-Fetch-Site` when it sends no `Origin`, names another site, by the rule and the allowed origins of the Server Actions (`EQuantic:ServerActions:AllowedOrigins`).

#### Scenario: A creation from another site

- **WHEN** a request with `Origin: https://evil.example` creates an upload, or sends a part to one, carrying the owner's cookie
- **THEN** it is answered 403, and the folder and the upload's offset are unchanged

#### Scenario: An origin the app allows

- **WHEN** the app allows `https://admin.example` for its actions and a request from it creates an upload
- **THEN** the upload is created
