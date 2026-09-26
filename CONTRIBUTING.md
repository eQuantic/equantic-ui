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
The one exception is outside the build: the change workflow runs the OpenSpec CLI, which needs
Node >= 20.19, and `./scripts/openspec.sh` installs the pinned version from its lockfile on first
use. CI validates your change either way.

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

**The build has no warnings, and fails on one.** `TreatWarningsAsErrors` is set in
`Directory.Build.props`, so a warning is a broken build rather than a line nobody reads — which is
what it had been: the tree shipped ~170 of them, and the only person who ever read one was a
consumer building the SDK from source. Where a rule genuinely does not apply, silence it at the
narrowest scope that works (`NoWarn`, or `WarningsNotAsErrors` to demote it while it keeps
printing), always beside a comment naming the rule and the reason. Two are repo-wide and say why
where they are declared: `CS1591` in `Directory.Build.props`, and `NU5128` in
`Directory.Build.targets`, conditioned to the packages that ship no assembly by design. This lists
every one of them:

```bash
grep -rn "NoWarn\|WarningsNotAsErrors" --include="*.csproj" --include="*.props" --include="*.targets" .
```

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
[Upgrading](https://github.com/eQuantic/equantic-ui/wiki/Upgrading) page, in both languages.

**You do not have to remember which contracts you broke — the build does.** Every project that
ships an assembly declares its public surface in `PublicAPI.Shipped.txt`, and
[PublicApiAnalyzers](https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
fails your build when what you wrote no longer matches it: RS0016 names a signature that appeared,
RS0017 one that went. After changing a public signature on purpose, declare it:

```bash
./scripts/public-api.sh update
```

It reads both diagnostics out of the build and writes `PublicAPI.Unshipped.txt` — an addition as
itself, a retirement as `*REMOVED*<entry>`. **Read that diff before committing it: it is the API
review**, and a line you did not mean to add is a public surface you did not mean to ship.

At a release `./scripts/public-api.sh ship` folds Unshipped into Shipped, and the `*REMOVED*` lines
it cancels ARE the release's list of breaks — with
`git diff v<previous>..v<this> -- '**/PublicAPI.Shipped.txt'` as the full change of surface. The
analyzer says what moved; the migration line is still yours to write, from what an app upgrading
actually met.

## Commits and pull requests

The working agreement is the **Workflow** section of [CLAUDE.md](CLAUDE.md), the same text as in
[AGENTS.md](AGENTS.md); what follows is the short version, and where the two differ that section wins.

`main` is protected: every change arrives through a pull request that closes an issue on the
[board](https://github.com/orgs/eQuantic/projects/11), is reviewed by its author and then by GitHub
Copilot (three rounds at most, stopping at the first without a defect), and is squash-merged when
every review thread is resolved and CI is green.

**Check that CI actually RAN before you read it as green.** Since 2026-09-26 the ruleset requires
thirteen of the CI's jobs and a branch up to date with `main` (#287), so a pull request whose
workflow never started stays blocked, and one that `main` moved past has to take `main` and run
again. Before that it required only a review: a workflow whose `if:` expression did not parse
failed before it created a single job, with no log to notice, and for an hour seven pull requests
merged on local runs alone. Two pull requests that each passed alone also broke `main` together
(#453).

```bash
scripts/ci-doctor.sh
```

It answers the two questions the pull-request page cannot: does GitHub still call the workflow `CI`
(it falls back to the file's path when it cannot read it), and did the run for this branch create
any jobs at all.

- **Start from an issue.** Every change has one on the board; if none exists, create it as a
  sub-issue of the epic or feature it belongs to, with the right type.
- **Branch first**, from `main`, named `<type>/<slug>` (`feat/`, `fix/`, `chore/`, `refactor/`,
  `docs/`, `test/`, `ci/`, `perf/`, `build/`). Never commit onto `main` locally either.
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
  | build | 📦 | the build system and dependencies |
  | style | 💄 | formatting |

  A breaking change is `✨ feat!: …` with a `BREAKING CHANGE:` paragraph in the body.
- **No attribution** in a commit or a pull request: no co-authorship line for an assistant, no
  session link, no generated-with footer.
- **The PR title follows the same format** — a squash merge takes it as the commit subject. The body
  says `Closes #N`, what changed, why, and what you ran; the template asks for exactly that.
- **A change that creates or changes behaviour starts with an OpenSpec proposal** in the same pull
  request (`/opsx:propose`, under `openspec/changes/`), archived before the merge.
- **Review the diff yourself before opening the PR** (in Claude Code, `/code-review high`), then
  **answer Copilot's rounds**. The first starts when a PR that is not a draft opens. A defect is
  fixed, proved both ways and earns another round, asked for (`gh pr edit <n> --add-reviewer
  @copilot`) after one push with every fix. Hardening, docs or a nit is fixed in the same push or
  filed as an issue, and a wrong finding is answered with what shows so: neither earns a round. The
  loop stops at the first round without a defect, and after three in any case. A PR is not done
  when it is opened.
- **Documentation and the ledger change with the code**: the Markdown here, the wiki in English and
  Portuguese on a wiki branch named like the pull request's, merged into the wiki's master when the
  pull request merges, and one `docs/LEDGER.md` line citing the issue.
- **Do not open thin PRs.** Group a coherent body of work — a slice, a family of fixes, a refactor and
  the test that proves it — so it can be reviewed as a unit.

## Documentation

The wiki is bilingual, and it is its own repository
([equantic-ui.wiki](https://github.com/eQuantic/equantic-ui/wiki), not a folder of this one). Every
page has an English canonical file and a Portuguese twin under `locale/pt-BR/<Page>-pt-BR.md` there,
edited in the **same wiki commit**; the documentation site fails its build
when a translation is older than its canonical page. That commit goes on a wiki branch named exactly
like the pull request's branch: CI reads it for the docs guards (`scripts/checkout-wiki.sh`), and it
is merged into the wiki's master when the pull request merges. To run the guards locally against
that branch, point `EQ_WIKI_DIR` at a worktree of it and leave the clone beside the repository on
master, since every local run reads that one. Version marks (`*Since **0.2.0-preview.N***`)
are derived from git, never from memory. The project's word is *component* — never "widget".

## Reporting

Bugs and feature requests: the [issue templates](https://github.com/eQuantic/equantic-ui/issues/new/choose)
ask for what a fix needs. Questions and ideas: [Discussions](https://github.com/eQuantic/equantic-ui/discussions).
Security problems: privately, through [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
