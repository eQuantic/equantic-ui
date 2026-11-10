## 1. A sorted set holds .NET's red-black tree

- [x] 1.1 Read .NET 10's `SortedSet<T>` and `TreeSubSet`: insertion, removal, construction from a sorted array, the copy, the breadth-first walk and a view's range
- [x] 1.2 The runtime: `utils/sorted-tree.ts`, each function the method it is named after, and `SortedSet` over it, its constructor's path and its views' included
- [x] 1.3 Prove it on both sides in `CollectionMemberConformanceTests`, failing on main, and the tree's invariants in `sorted-tree.spec.ts`

## 2. Documentation and the suites

- [x] 2.1 The wiki's SupportedFeatures page in every language it is written in, on the wiki branch of this pull request
- [x] 2.2 A ledger entry under `docs/ledger/`, citing #766
- [x] 2.3 The suites, each read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
