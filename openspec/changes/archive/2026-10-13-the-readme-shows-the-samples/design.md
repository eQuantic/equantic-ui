# Design

## Context

See proposal.md. The three samples already open any of their screens by configuration: a route for
the dashboard, `--section` for the gallery, `screen` for the Wallet. The macOS shell already draws
one settled frame with no window (`--Photon:ScreenshotPath`), and `scripts/check-ios-launch.sh`
already makes a simulator of its own. Flutter has no part to ask: this is how the repository keeps
its own front page true.

## Goals / Non-Goals

**Goals:**

- Pictures anyone can take again, with nothing to click.
- A guard that fails when a pictured screen changes, and says which picture and how to take it.
- Pictures that draw wherever the README is read: on GitHub on any branch and in a fork, and on
  nuget.org.

**Non-Goals:**

- Comparing pixels. A frame drawn again and compared with the committed picture would hold a
  component's look too, and it would differ by the fonts and the browser of the machine that drew it.
- A video in the page. GitHub plays only a file uploaded through its own editor, whose address a
  README then names, and it gives that file a player. An animated picture is not the stand-in: see
  "Every picture is still".

## Decisions

**The guard holds files, not pixels.** A picture's entry names the sample's files its screen is
written in, each with the SHA-256 it had when the picture was taken. The test reads the bytes, so it
runs on every runner with no browser and no simulator. What it does not see is a change to a
component the screen places, which is no file of the sample: a redrawn button reaches the pictures
the next time one of them is taken.

**Recording is a separate act from taking.** `EQ_UPDATE_README_CAPTURES=1` writes the digests and
nothing else. It is one command, so the failure's message and the record's own comment both say to
take the picture and look at it first. A step that recorded as it captured would make the guard pass
over whatever the capture drew.

**Nothing is clicked.** Every pictured screen is one the sample opens by configuration, so a capture
is a launch and a wait. A screen reached only by a press is not pictured.

**A capture that drew nothing is refused.** The first launch of an app after its install is slow, and
two of the first six iOS captures were a black screen under a status bar. The script spends that
launch before the first picture, and every capture is read: one with almost no colour under its top
twelfth is refused where it is taken. An emulator booted with no window is asleep, and its screen
photographs as black too, so each Android photograph wakes it.

**Every picture is still.** The page opened with a tour of the dashboard's pages, a GIF of ten
pages that looped for good. Something that moves by itself for more than five seconds needs a way to
pause it (WCAG 2.2.2). GitHub gives one, and marks the image `data-animated-image` for it, but
nuget.org gives an animated image nothing, and the README is every package's readme there. So the
page opens with the dashboard's first page, still, whose sidebar names the rest, and a test refuses a
picture that moves: a GIF, or a PNG with an animation chunk. Motion comes back as a video, with the
player GitHub gives an uploaded file.

**A page that never finishes loading has no picture.** A headless Chrome writes its picture when the
page has loaded, and the dashboard's Live page keeps a connection open: it held the browser for ten
minutes. Each capture has forty seconds, and that page is not pictured.

**A path in the README, an address in the package.** The first version named each picture by its
address on `main`, and on the change's own branch the README showed ten broken pictures: the files
were on the branch and the addresses were not. A path in the repository is what GitHub draws on every
branch, in a fork and in the pull request that takes a picture again. nuget.org draws nothing from a
path, and it shows a readme's HTML as text: on the page of 0.2.0-preview.61 the README's own badges
are there as `&lt;img src`. It does draw a Markdown image from `raw.githubusercontent.com`, as the
pages of Refit and Serilog show. So the README writes `![what it shows](assets/readme/<file>)`, and
a package's readme is written when the package packs: the README with each of those paths turned
into the picture's address under `v<the package's version>`. That tag is the release's own, and a
tag's address answers (asked of `v0.2.0-preview.61`), so a package's page goes on showing the screens
of its version whatever `main` shows later. Pictures that sit side by side are the cells of a table,
whose header row says what each one is. The title and the badges above them stay as they were, and
what to do about them is #882.

**A program writes the package's readme, and the README's text never crosses MSBuild.** Handing the
text to MSBuild's own tasks was tried, and on macOS it packed a different README. A backslash in an
item is taken for a path separator, so the README's `\e` became `/e`. `WriteLinesToFile` cuts an item
at every semicolon and reads the `%20` of a badge's address as a space. And a backslash given to a
property function as an argument arrives as a slash: a replacement meant for the README's one
backslash changed its 365 slashes. So `Directory.Build.targets` declares a task in C#, which MSBuild
compiles when a project first packs. It reads the README, replaces the paths and writes the copy,
0.3 seconds a process. `ReadmeCapturesTests` runs the target and compares what it wrote with the
README byte for byte, but for the addresses.

**The readme is read inside every package.** A target can be right and not run for a project, and the
per-OS runtime packages are packed on Linux and on Windows. `scripts/check-package-readme.cs` opens
each package CI packed and compares its readme with the README, the addresses at that package's own
version.

**A simulator of its own, and the emulator as found.** The iOS target makes a simulator on the newest
runtime installed and deletes it, as the launch check does, so a developer's own is never touched.
The Android target uses the emulator that is running, and what is on it is its owner's. It replaces
no Wallet already installed, uninstalls the one it installs, and puts back what it changes from what
it read first: the night mode, whether the status bar may be staged, a screen that was off, and
`debug.mono.env`, which is a developer's own list of variables with `|` between them and goes back
quoted for the device's shell.

## Risks / Trade-offs

- The pictures are about 2 MB in the repository, and each new take adds its bytes to the history.
- A package packed at a version that has no tag names addresses nothing answers at: a local pack, a
  pull request's packages. Those readmes are read by no one, and a release's tag is pushed before
  its packages are published.
- nuget.org drawing these pictures is first seen on the page of the release after this change.
- The Android target needs an emulator with room for the app. One short of storage refuses the
  install, and `EQ_README_ANDROID_INSTALLED=1` takes the pictures from a build already on it.
