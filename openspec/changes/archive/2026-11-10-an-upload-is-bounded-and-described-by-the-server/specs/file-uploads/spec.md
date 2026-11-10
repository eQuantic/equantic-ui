## ADDED Requirements

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
