# Tasks

## 1. The generator names the source relative to the shader

- [x] 1.1 Run `slangc` from the shader's folder with the source's name. Verify: the regenerated `Sdf.metal` differs from the committed one only in its `#line` paths, and every SPIR-V output is byte for byte the same.

## 2. A guard that fails

- [x] 2.1 `GeneratedShaderPathTests` reads every generated output. Verify: both cases fail on main's `Sdf.metal`, naming `/Users/`, and pass after the regeneration, beside the Metal tests that compile the MSL at run time (30 of 30).

## 3. Documentation

- [x] 3.1 A ledger entry under `docs/ledger/` citing #1416. No wiki page describes the generator's paths.
