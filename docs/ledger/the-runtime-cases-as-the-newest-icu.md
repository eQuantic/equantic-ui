- **2026-10-24 · The runtime cases as the newest ICU does**: `ubuntu-latest` moved to Ubuntu 26.04, whose
  ICU is 78, Unicode 17, and .NET 10 there upper-cases `ƛ` to `Ƛ`, where the runtime kept the letters
  Unicode 16 and 17 cased as the servers of Ubuntu 24.04 and Windows keep them: 48 cases of
  `CasingConformanceTests` failed on `main` and on every pull request. Edgar's decision was the newer
  Unicode: the runtime's list of the code points to keep their case became the list of their case pairs,
  written out so that a browser whose own Unicode is older answers as the servers do, and the 48 cases
  pass on macOS 26 and fail there on main's runtime. On a host whose ICU is older the suite leaves those
  letters out of the cases that read every code point and compares every other one
  ([#1032](https://github.com/eQuantic/equantic-ui/issues/1032)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-bcl`).
