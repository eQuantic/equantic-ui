# Tasks

## 1. The Windows shell

- [x] 1.1 Each shipped file is made into a collection of its own when it registers, and the face it holds read
      there: the name DirectWrite gives its family and the weight it reads in the file
- [x] 1.2 A family that ships files is set from the collection of the file `ShippedFaces.FileFor` chooses,
      under the theme's name when the file carries it and the file's otherwise, a static file asked for its
      own weight, or a bold one for a heavier weight, at the weight DirectWrite reads in it
- [x] 1.3 Check, on the Windows runner, three ways in one run: with the change, the family of 300, 400 and 700
      measures the widths it measures on Apple, from one table; two files that declare 250 and that the catalog
      gives 100 and 200 measure two widths; a file the catalog weighs at 700 that declares 400 draws in `Bold`
      and in `Black` as it draws at 400; a family named otherwise than its file is set in it. With the shell as
      it was, the last two fail and the others pass; with the change but the weight asked as it is, the
      catalog's weight fails alone

## 2. Documentation

- [x] 2.1 The wiki's DesignSystem page already says a shell sets text in the file of its weight and slant;
      it holds on Windows in every case now, and no page changes
- [x] 2.2 One ledger entry citing #1099, as a file of its own under `docs/ledger/`
