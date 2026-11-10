# Promotion

The material that shows eQuantic.UI outside this repository, versioned with what makes it: the
motion and audio projects, the generated assets, and the scripts that record, build and measure.
Every piece is made from the SDK's own samples, so it can be made again when a screen changes.

Each piece is a folder, and its README is the recipe. What a piece produces (raw recordings,
renders) stays out of git: it is reproducible, and heavy.

| Piece | What it is |
|---|---|
| [wallet-video](wallet-video/README.md) | The Wallet sample on iOS, Android, the web and macOS in one continuous camera move, 4:5 and 9:16, with its own score |

The pieces are made in the Craft apps (EffectCraft for motion, SoundCraft for audio), driven through
their MCP servers. Nothing here is part of the SDK's build or its tests.
