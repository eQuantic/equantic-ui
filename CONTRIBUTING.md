# Contributing to eQuantic.UI

Thank you for wanting to work on this. The project is in preview, moves fast, and is measured rather
than remembered: most of what a contributor needs to know is written down where a test can check it.
This page says where.

## What to work on

- **Issues labelled [`good first issue`](https://github.com/eQuantic/equantic-ui/labels/good%20first%20issue)**
  are small, self-contained, and come with the file paths, the measurement and the acceptance
  criteria already written. **[`help wanted`](https://github.com/eQuantic/equantic-ui/labels/help%20wanted)**
  is the same, larger.
- **[docs/ARCHITECTURE-AUDIT.md](docs/ARCHITECTURE-AUDIT.md)** ends with an *order of attack*: every
  open structural item, measured and sized, with the Flutter answer it follows. If you want to take
  one, say so on its issue or open one, so two people do not take the same step.
- Correctness of the transpiler, layout parity between the web and Photon realizers, the Photon
  engine and its shells, accessibility bridges, and the write-once contract on more host and target
  combinations are the areas where help matters most.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and nothing else — no Node.js, no
npm. The TypeScript runtime is built by an embedded Bun the build extracts itself, and the Photon
shaders are committed (only framework developers changing a shader run `scripts/generate-shaders.sh`).

```bash
git clone https://github.com/eQuantic/equantic-ui.git
cd equantic-ui
dotnet build                               # the whole solution
dotnet test                                # every test project
dotnet build samples/DefaultUIDashboard    # a web app against the source tree
dotnet build samples/PhotonDesktop         # a Photon app (macOS)
```

The samples build against the framework's **projects**, never against packages — see the wiki's
[Build Flow](https://github.com/eQuantic/equantic-ui/wiki/BuildFlow). CI does not build them, so a
change that touches the SDK, the compiler or a realizer is proved by building them locally.

The TypeScript runtime has its own suite, and the same rule holds — no Node, no npm. One MSBuild
target runs it through the embedded Bun the build extracts, on every OS: it installs the pinned
dependencies from `bun.lock`, type-checks with `tsc` (the check that makes an exhaustive `switch`
fail to compile when a case is missing — the bundle is produced without it), then runs `vitest run`
once and exits:

```bash
dotnet build src/eQuantic.UI.Runtime -t:TestRuntime
```

## The bar

Three rules decide most reviews here; they are in [CLAUDE.md](CLAUDE.md) at length.

1. **A developer using the SDK never writes a platform artifact.** No Swift, no plist, no manifest, no
   CSS. If a feature works but makes someone learn one, it is not done.
2. **The vocabulary speaks no target's language.** A node or a property in `eQuantic.UI.Primitives`
   is named as Flutter would name it, never as HTML or Apple would — `Label`, not `Alt`; `Layer`, not
   `ZIndex`. The DOM escape hatch (`HtmlElement`) is the one place where the web's words are right.
3. **Prefer the instrument that fails to the one that warns.** A pin that compares and fails,
   regenerated behind an environment variable, beats a warning; a structural fix beats a patch; a
   mechanism .NET already has beats a home-grown one.

We are in preview: **break contracts freely and leave nothing behind.** Finish the refactor, delete
what stopped being used, and write the migration note — every breaking change goes into the release
notes (the annotated tag's message) and the wiki's
[Upgrading](https://github.com/eQuantic/equantic-ui/wiki/Upgrading) page.

## Commits and pull requests

`main` is protected: every change arrives through a pull request, reviewed by GitHub Copilot
automatically and merged when the review threads are resolved and CI is green.

**Check that CI actually RAN before you read it as green.** The ruleset requires a review, not a
status check, so a pull request whose workflow never started still reads mergeable — and a workflow
whose `if:` expression does not parse fails before it creates a single job, with no log to notice.
That happened here for an hour and seven pull requests merged on local runs alone.

```bash
scripts/ci-doctor.sh
```

It answers the two questions the pull-request page cannot: does GitHub still call the workflow `CI`
(it falls back to the file's path when it cannot read it), and did the run for this branch create
any jobs at all.

- **Branch first**, from `main`. Never commit onto `main` locally either.
- **Commit messages** are `emoji type: description`, in English, emoji first:

  | Type | Emoji | Use |
  |---|---|---|
  | feat | ✨ | a new feature |
  | fix | 🐛 | a bug fix |
  | docs | 📝 | documentation |
  | refactor | ♻️ | restructuring without behaviour change |
  | test | ✅ | tests |
  | chore | 🔧 | maintenance |
  | ci | 👷 | the pipeline |
  | perf | ⚡ | performance |
  | style | 💄 | formatting |

  A breaking change is `✨ feat!: …` with a `BREAKING CHANGE:` paragraph in the body.
- **The PR title follows the same format** — a squash merge takes it as the commit subject. The body
  says what changed, why, and what you ran; the template asks for exactly that.
- **Read the Copilot review** and address it: fix, or reply saying why not. A PR is not done when it
  is opened.
- **Do not open thin PRs.** Group a coherent body of work — a slice, a family of fixes, a refactor and
  the test that proves it — so it can be reviewed as a unit.

## Documentation

The wiki is bilingual, and it is its own repository
([equantic-ui.wiki](https://github.com/eQuantic/equantic-ui/wiki), not a folder of this one). Every
page has an English canonical file and a Portuguese twin under `locale/pt-BR/<Page>-pt-BR.md` there,
edited in the **same wiki commit**; the documentation site fails its build
when a translation is older than its canonical page. Version marks (`*Since **0.2.0-preview.N***`)
are derived from git, never from memory. The project's word is *component* — never "widget".

## Reporting

Bugs and feature requests: the [issue templates](https://github.com/eQuantic/equantic-ui/issues/new/choose)
ask for what a fix needs. Questions and ideas: [Discussions](https://github.com/eQuantic/equantic-ui/discussions).
Security problems: privately, through [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
