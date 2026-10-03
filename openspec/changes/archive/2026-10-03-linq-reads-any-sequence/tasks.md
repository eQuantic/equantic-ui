# Tasks

## 1. Sequences

- [x] 1.1 Read every LINQ operator's source through one place, `$eq.linq.seq` for anything that is not an array. Verify: `LinqOverAnySequenceConformanceTests.ACollectionOfItsOwnClass_IsASequence` and `ADictionary_IsASequenceOfItsPairs`, failing against main
- [x] 1.2 A string's chars by code unit, `new string`, and a char static's method group. Verify: `AString_IsASequenceOfCodeUnits` and `AUsingStaticChar_IsTheSameMethod`, failing against main

## 2. Copies

- [x] 2.1 `ToList` and `ToArray` make a new array, always. Verify: `WhatLinqMaterializes_IsACopy`, failing against main, the read-only face and the pass-through operators included

## 3. Documentation and the suites

- [x] 3.1 The transpiled library's pins regenerated and read: a copy where it calls `ToList`, and a line's chars by code unit
- [x] 3.2 The wiki's SupportedFeatures page (EN + pt-BR) on the branch named like this one
- [x] 3.3 The suites, each alone and read by its exit code: Compiler, Web, Server, Conformance and the runtime's `TestRuntime`
