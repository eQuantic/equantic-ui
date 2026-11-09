## ADDED Requirements

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
