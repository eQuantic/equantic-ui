- **2026-11-26 · Every change that grows the served runtime records its delta, however small**: growth under the
  budget's 1% margin passed unrecorded, so the first change past it was handed everyone's bytes, as #1913 was. A change
  is now measured against the commit it is measured from, whose runtime the budget test builds as the Server does, and
  a change that grows or shrinks it by any byte fails until it records what it moved. The record is that growth, the 1% stays as
  the margin for noise, and a release's fold writes every delta as a row of `docs/SERVED-RUNTIME-GROWTH.md` with the
  pull request that added it ([#1922](https://github.com/eQuantic/equantic-ui/issues/1922)). Proposed and archived
  through OpenSpec (`openspec/specs/working-agreement`).
