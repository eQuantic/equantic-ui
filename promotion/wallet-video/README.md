# The Wallet video

Thirty seconds, two formats (4:5 for the feed, 9:16 for stories): the same screens of the Wallet
sample, written once in C#, running at the same moment in Chrome, the iPhone simulator, the Android
emulator and a native macOS window. A camera flies from device to device in one continuous move,
each device is labelled with what it does not need (Swift, Kotlin, JavaScript, Electron), and the
cut lands on a score written for its grid.

## What is here

| Path | What it holds | Made by |
|---|---|---|
| `effectcraft/wallet.ecproj` | The motion project: every composition, the camera, the render queue | EffectCraft |
| `effectcraft/footage/` | The four recordings at 30 fps, the code card, the logo, the score | the steps below |
| `effectcraft/models/` | The device bodies, glTF | `tools/models.sh` |
| `soundcraft/wallet-agency.scsession` | The score: instruments, effects, the mix | SoundCraft |
| `soundcraft/score.mid` | The score's notes | `tools/score.py` |
| `tools/` | Every script the steps run | |

The project's paths are relative to the project, so it opens from any checkout. Renders go to
`effectcraft/out/` and raw recordings to `raw/`, both ignored by git.

## The compositions

| Composition | Role |
|---|---|
| `Wallet Agency 4x5`, `Wallet Agency 9x16` | The cuts: the hook, the 3D scene, the end card and the score |
| `SF Hook`, `SF Hook 9x16` | "Write it once." over the code card; its first frame is the cover a feed shows |
| `Hero 3D`, `Hero 3D 9x16` | The four devices as 3D objects, the camera move, the labels, the closing title |
| `SF End`, `SF End 9x16` | The logo, the site and the repository |
| `Device iPhone`, `Device Android`, `Device Browser`, `Device Mac` | Each recording in its frame |
| `Screen Monitor`, `Screen Laptop`, `Laptop Deck` | What the monitor and the laptop show, and the laptop's keyboard |
| `Code Card` | The code card |

The cut sits on the score's grid: 133.33 BPM, a beat is 0.45 s and a bar 1.8 s. The hook is bars
0-1, each device holds two bars and travels on the last two beats of them, the pull-back lands on bar
10 with the closing title, the logo on bar 12. A change of timing moves the camera's keys in
`Hero 3D` and the bars in `tools/score.py` together.

## Making it again

The tools are Swift and Python with nothing to install beyond Xcode's command line tools: no
ffmpeg, no Pillow, no numpy. Compile the Swift ones once:

```bash
for tool in probe changes trim sheet windows winrec; do swiftc -O -o "tools/$tool" "tools/$tool.swift"; done
```

### 1. Record the four targets

The Wallet sample has a tour mode: `WalletApp` walks its own screens on an `IClock`, a 19-second
loop, and nobody touches anything, which is what keeps the four recordings in step to within a
frame. On native it is the configuration value `tour`, on the web the `/tour` route.

| Target | Start in tour mode | Record |
|---|---|---|
| iOS | `SIMCTL_CHILD_tour=true xcrun simctl launch <device> <bundle id>` | `python3 tools/record2.py 50 raw/ios.log xcrun simctl io <device> recordVideo raw/ios-tour.mov` |
| Android | `adb shell setprop debug.mono.env 'tour=true'`, then start the app | `adb shell screenrecord --time-limit 45 --bit-rate 16000000 /sdcard/wallet-tour.mp4`, then `adb pull` it to `raw/android-tour.mp4` |
| macOS | build `samples/WalletMobile` for the desktop, then from its output folder `dotnet WalletMobile.dll --tour true --Photon:Width 1100 --Photon:Height 720` (a window of that size opens the desktop layout, with its rail) | `tools/windows Wallet` for the window id, then `tools/winrec <id> 50 raw/mac-tour.mov` |
| Web | `dotnet run --project samples/WalletWeb`, open `/tour` | a screencast of the page at a 1000x625 viewport, device scale 2 |

`record2.py` stops the recorder with SIGINT after the time given, because `simctl recordVideo` only
finishes a playable file when interrupted. When `simctl` leaves a file that never finishes, restart the
simulator: it happens after hours of uptime or under heavy load.

### 2. Cut each recording to one loop

`tools/changes <recording>` lists the moments the picture changes; the loop starts at the first
change of the first screen. Then cut 19 seconds from there, at a constant 30 fps:

```bash
tools/trim raw/ios-tour.mov 10.60 19.0 604 1312 effectcraft/footage/ios.mp4
tools/trim raw/android-tour.mp4 15.10 19.0 540 1200 effectcraft/footage/android.mp4
tools/trim raw/mac-tour.mov 12.14 19.0 1100 748 effectcraft/footage/mac.mp4
tools/trim raw/web-tour-1000.mp4 18.98 19.0 1280 800 effectcraft/footage/web1000.mp4
```

The start times are this recording's; a new recording has its own. `tools/sheet <clip> <png> <columns>
<cell width> <seconds>...` makes a contact sheet of one clip: sheets of the four at the same seconds
show whether they reach each screen together.

### 3. The code card and the models

`tools/code.html` is the code card: open it in a browser and save the card, 940x500 at device scale
2, as `effectcraft/footage/code.png`. `tools/models.sh` writes the device bodies into
`effectcraft/models/` (it reproduces the committed ones byte for byte). Each body is a rounded slab,
in pixels of the comp, centred on the origin. The phones, the monitor and the laptop's lid have no
front cap, so nothing of the body stands in front of the screen that sits on it.

### 4. The score

`python3 tools/score.py soundcraft/score.mid` writes the notes. In SoundCraft, `soundcraft/wallet-agency.scsession`
holds them already with the instruments, the effects and the master chain. Bounce the mix to
`effectcraft/footage/score.flac`, 16-bit, from 0 to 27.6 s. `python3 tools/envelope.py <wav>` prints
the loudness over time, to check the mix against the cut without listening to it. It was made at
-15.4 LUFS integrated with the true peak under -1 dB.

### 5. Render

Open `effectcraft/wallet.ecproj` and render the queue. It holds the two cuts, written to
`effectcraft/out/`. The 3D scenes use Advanced 3D with depth of field and take about five minutes a cut on
the CPU.

To bring the project back into the repository after working on it elsewhere, use EffectCraft's
File > Dependencies > Collect Files into `effectcraft/`, then `python3 tools/relocate.py effectcraft`.
It moves the media into `footage/` and `models/` and makes every path relative again. It fails
when an absolute path is left.

## What EffectCraft taught the hard way

- A glTF import fits the model to the frame (Model Scale about 0.52). Set `geometryOptions/unitScale`
  to 1 so the generator's pixels are the comp's.
- `file.reloadFootage` does not reload a changed glTF: write it under a new name and use
  `file.replaceFootage`. Replacing can turn the 3D model layer into a plain footage layer, which then
  draws nothing. Add the item again as a new layer.
- A precomposition parented to a null follows it in Advanced 3D since 0.23.1, and the screens hang
  from their device's rig that way.
- Depth of field measures a layer two parents deep (the laptop's screen hangs from a hinge that
  hangs from the rig) at the wrong depth, so the laptop's shot closes the aperture.
- A camera's position and point of interest interpolate along curves by default. Linear spatial
  interpolation keeps the point of interest on the device between keys, and the focus with it.
- A screen is a light source: its layer does not accept the scene's lights, or it reads as printed
  paper.
