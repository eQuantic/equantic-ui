# Tasks

## 1. eqc

- [x] 1.1 `CollectionExpressionStrategy` builds a dictionary, a queue, a stack and a linked list target through
      the class's own empty construction, with the capacity `with(…)` hands the constructor, and refuses an
      element with EQ1004
- [x] 1.2 `DictionaryStrategy.Empty` and `QueueStackStrategy.Empty` construct as `new()` and `new(capacity)` do,
      a sorted dictionary or list dropping the capacity
- [x] 1.3 A local typed as a dictionary and started with `[]` is annotated `any`, as `new()`'s is
- [x] 1.4 Check with the conformance suite, both sides executed: the six targets started with `[]` and
      `[with(…)]`, in a local, a property, an object initializer and a conditional, fail before the change and
      pass after it; the inherited names through every construction form, and through the code diff engine,
      pass on both

## 2. Documentation

- [x] 2.1 No wiki page lists which targets a collection expression builds, and no page changes
- [x] 2.2 One ledger entry citing #1144, as a file of its own under `docs/ledger/`
