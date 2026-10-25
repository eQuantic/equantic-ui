# Tasks

## 1. The vocabulary

- [x] 1.1 `GoogleFontAttribute`, on the assembly and as many times as an app likes: a family, the weights
      as `FontWeight` members (`Regular` when none is given), and `Italic`
- [x] 1.2 Check: the public surface declares it (`PublicAPI.Unshipped.txt`)

## 2. eqicon fetches, pins and keeps the files

- [x] 2.1 The declarations, read from the syntax of the files MSBuild hands over: a family literal, weights
      as `FontWeight` members, `Italic` as `true` or `false`, anything else EQ4007 at its place
- [x] 2.2 Google Fonts' `css2` API, asked as a client that is not a browser, one request a family; on a 400,
      the family alone and then each cut, to name what is missing (EQ4008); an unreachable provider with a
      file still to fetch fails the same way
- [x] 2.3 `fonts.lock.json`: written when a file is first fetched, read on every build; a pinned file
      fetched from its pinned URL and checked against its SHA-256; a mismatch, a vanished URL or a family
      the provider moved on from fails (EQ4009), naming the way to renew; a family the app stopped
      declaring leaves the lock
- [x] 2.4 The files kept under `obj/equantic/fonts/google`, one a cut, named by the family and the cut,
      checked against their pins on every build and written whole or not at all; a build that fails
      leaves the lock and the folder as they were
- [x] 2.5 The folder is what MSBuild embeds: nothing but the files the declarations want is left in it
- [x] 2.6 Check: eqicon's tests against a provider served from test fixtures, both ways, for every outcome
      above

## 3. The SDKs embed them

- [x] 3.1 A target in both SDKs, before `AssignTargetPaths`, that hands eqicon the files mentioning
      `GoogleFont` and embeds what it lists, with `EQuanticFont` set, so the catalog reads them
- [x] 3.2 Design-time builds fetch nothing and embed what the last build fetched
- [x] 3.3 Check: a probe web app declaring a Google family, built for real: the catalog names it, the page
      sets text in it from `/_equantic/fonts/` and asks Google for nothing (in a browser), a rebuild
      fetches nothing, a lock whose hash was changed fails the build, and taking the family out of the
      lock renews it
- [x] 3.4 Check: a Photon app declaring the same family holds its files in its own assembly

## 4. Documentation

- [x] 4.1 The wiki's DesignSystem page in all five languages: a face from Google Fonts, its lock, and how
      to renew it
- [x] 4.2 EQ4007, EQ4008 and EQ4009 in `docs/DIAGNOSTICS.md` and in the wiki's Diagnostics page in all
      five languages
- [x] 4.3 One ledger entry citing #1057, as a file of its own under `docs/ledger/`
