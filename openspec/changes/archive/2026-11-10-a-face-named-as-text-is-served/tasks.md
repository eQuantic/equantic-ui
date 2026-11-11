# Tasks

## 1. A face outside the theme's stylesheet is named by its identity

- [x] 1.1 A page names such a face at `/_equantic/fonts/<assembly>/<resource>?v=<hash>`, and the identity route sends the bytes at the file's version and redirects otherwise. Verify: `AFamilyThatNamesItsAssemblyOnlyAsText_IsDeclared_AndServedByEveryInstance_ThoughNoListCarriesIt`, `APageOneInstanceRendered_IsServedByAnotherThatNeverRenderedIt` and both cases of `AFaceAskedForByItsIdentity_IsSentToItsVersion` fail on main and pass here
- [x] 1.2 The route loads only an exact simple name outside the platform. Verify: the three new cases of `WhatNoEQuanticBuildEmbeddedAsAFace_IsNotFound` (another spelling, a display name, a path) answer 404
- [x] 1.3 The deployment scan is gone, and the content-addressed route serves the theme's stylesheet alone. Verify: the font serving suite passes, 25 of 25, and the Server suite passes

## 2. Proved against the real thing

- [x] 2.1 A web app published as a single file over a library it names only as text, two instances of the binary. Verify: the first declares the family with its identity, the second, which never rendered the page, answers that URL with the library's bytes and the bare identity with a redirect to it; on main the page declares nothing, the identity answers 404 and the server logs that no build embedded the face
- [x] 2.2 Docs. Verify: the wiki's DesignSystem page says how a face outside the theme is addressed, in all five languages, and a ledger entry cites #1080
