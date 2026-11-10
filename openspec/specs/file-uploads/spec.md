# file-uploads Specification

## Purpose
How files reach an app's server: sent by a page through `IUploads` or the `FileUpload` component, in resumable parts under a policy the app declares and the page checks first, kept for their owner alone in a staging folder that expires, and handed to a Server Action as an `UploadedFile` it reads as a stream.

## Requirements

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

### Requirement: A page sends a file in resumable parts

`IUploads.Start(file, policy)` SHALL answer at once with an `Upload` that sends the `PickedFile` to the upload endpoint under the policy, in parts of at most 8 MB, and SHALL report its `State`, its `Sent` bytes, counting the part in flight, and its `Size`. Once the last byte arrived its `State` SHALL be `Completed` and its `Result` an `UploadedFile` whose id is the last segment of the upload's address, and the upload SHALL forget the address it remembered. `Subscribe` SHALL call its listener on every change from then on and stop when disposed.

#### Scenario: Three parts

- **WHEN** a page starts a file of 20 bytes under a policy that accepts it, with parts of 8 bytes
- **THEN** the browser creates the upload with `Upload-Length: 20` and the policy, the file's name and its type in `Upload-Metadata`, sends parts at the offsets 0, 8 and 16, and the upload completes with an `UploadedFile` of size 20 under the policy's name

### Requirement: A policy refuses a file before a byte is sent

`UploadPolicy.RefusalOf(fileName, size)` SHALL answer the reason the server gives for the first rule the file breaks, its format and then its size, or null when the policy accepts it, and `UploadPolicy.Accepts(fileName)` SHALL answer whether its name ends in one of the policy's extensions without regard to case. The browser SHALL answer them as .NET does. `Start` SHALL ask the policy first and, for a file it refuses, answer an upload that has `Failed` with that reason and sent nothing.

#### Scenario: Not one of the formats

- **WHEN** a page starts `setup.exe` under a policy of `stl` and `obj`
- **THEN** the upload has failed with "Only .stl, .obj files are accepted here." and the endpoint received no request

### Requirement: An interrupted part is sent again from where the server stands

A part that is not answered, or answered with a server error, an offset conflict (409) or a lock (423), SHALL be sent again after a pause of 0, 1, 3, 5 and then 10 seconds, each try asking the server's offset first, and past the last pause the upload SHALL fail with "The connection to the server was lost." Any other refusal SHALL fail the upload at once with the server's reason as its `Error`.

#### Scenario: A part that failed

- **WHEN** the first part gets no answer and the next is answered 503
- **THEN** the browser asks the offset before each new try, and the upload completes with the server holding every byte once

#### Scenario: The server refuses

- **WHEN** the creation is answered 429 with "At most 3 uploads may be unfinished at a time."
- **THEN** the upload has failed with that reason and nothing more was sent

### Requirement: A reloaded page goes on with the same file

The browser SHALL remember an unfinished upload's address under the policy and the file's name, size and date, and starting the same file under the same policy again SHALL ask the server's offset and send only the rest. An address the server answers 403, 404 or 410 for SHALL be forgotten and the file started over. A pause SHALL stop sending, and a resume SHALL go on from the server's offset.

#### Scenario: Reloaded halfway

- **WHEN** a page sent the first part of a file and is gone, and the same file is started again
- **THEN** the browser asks the offset of the remembered upload, creates none, and sends from that offset to the end

### Requirement: A cancelled upload is removed

`Cancel` SHALL stop the request in flight, set the state to `Cancelled`, forget the address, and ask the server to delete the upload.

#### Scenario: Cancelled after a part

- **WHEN** a page cancels an upload after its first part arrived
- **THEN** the server receives a DELETE for it and holds nothing of it

### Requirement: FileUpload is the area, the button and a row per file

`FileUpload` SHALL draw a drop area and a button that asks the picker for the policy's filters, start an upload under its policy for every file chosen or dropped (one when `multiple` is false), draw a row per file with its name, its state, a progress bar labelled with the file, its pause, resume and cancel controls and its error, announce in a live region each file that arrived or failed, and hand each arrival to `onCompleted` once. Its first build SHALL be the server's, and it SHALL be enabled only once mounted where both `IFilePicker` and `IUploads` are available.

#### Scenario: A file of three parts through the sample

- **WHEN** a person chooses a file of 20 MB on the sample's `/uploads` page
- **THEN** the page creates one upload and sends three parts, and the Server Action answers the file's exact length and SHA-256

#### Scenario: Where nothing can be sent

- **WHEN** the area is built without an `IUploads`, or before it is mounted
- **THEN** its button is disabled and its drop area is off

### Requirement: The uploads a server keeps are bounded

The server SHALL bound the uploads it keeps, finished or not, until they expire: per owner by
`MaxUploadsPerOwner` and `MaxBytesPerOwner`, and for the whole store by `MaxTotalUploads` and
`MaxTotalBytes`, all read from `EQuantic:Uploads`. A creation past a limit on uploads SHALL be refused with
429, and one past a limit on bytes with 413, each naming the limit. A limit below 1 SHALL stop the app at
start, and so SHALL a policy that accepts a file larger than a byte limit.

#### Scenario: A script that keeps no cookie

- **WHEN** the store keeps at most 3 uploads, and four browsers that send no cookie each upload a file
- **THEN** the fourth creation is refused with 429, and the store keeps three

#### Scenario: A finished upload counts

- **WHEN** an owner may keep two uploads and has finished two
- **THEN** a third is refused with 429, and once one is deleted a third is created

### Requirement: The server says what an uploaded file is

An uploaded file's type SHALL be the one its extension names, never the device's. Its name SHALL keep no
folder, no control character and no character a file system refuses, SHALL never be `.` or `..`, and
SHALL not be a name Windows keeps for a device. A folder under the web root SHALL stop the app at start,
and the owner's cookie SHALL be `Secure` but for plain HTTP to this machine, or in the Development environment.

#### Scenario: A page uploaded as a picture

- **WHEN** a file named `page.png` is uploaded with the device's type `text/html`
- **THEN** the action that receives it reads `image/png`
