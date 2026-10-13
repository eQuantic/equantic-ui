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
- Pictures that draw on nuget.org as they do on GitHub.

**Non-Goals:**

- Comparing pixels. A frame drawn again and compared with the committed picture would hold a
  component's look too, and it would differ by the fonts and the browser of the machine that drew it.
- A video in the page. GitHub plays only a file uploaded through its own editor, whose address a
  README then names. The tour is an animated picture meanwhile.

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
twelfth is refused where it is taken.

**A page that never finishes loading has no picture.** A headless Chrome writes its picture when the
page has loaded, and the dashboard's Live page keeps a connection open: it held the browser for ten
minutes. Each capture has forty seconds, and that page is not pictured.

**Markdown images, with full addresses.** The README is packed into every package, and nuget.org
shows a readme's HTML as text: on the page of 0.2.0-preview.61 the README's own badges are there as
`&lt;img src`, drawn by nothing. So a picture is `![what it shows](address)`, with the address
`raw.githubusercontent.com/…/main/assets/readme/<file>`, a host nuget.org draws from, and pictures
that sit side by side are the cells of a table, whose header row says what each one is. The title and
the badges above them stay as they were, and what to do about them is #882.

**A simulator of its own, and the emulator as found.** The iOS target makes a simulator on the newest
runtime installed and deletes it, as the launch check does, so a developer's own is never touched.
The Android target uses the emulator that is running and puts back its night mode, its status bar
and the property that names a screen.

## Risks / Trade-offs

- The pictures are about 2 MB in the repository, and each new take adds its bytes to the history.
- A picture's address names `main`, so on the pull request that adds one it draws nothing until the
  merge.
- The Android target needs an emulator with room for the app. One short of storage refuses the
  install, and `EQ_README_ANDROID_INSTALLED=1` takes the pictures from a build already on it.
