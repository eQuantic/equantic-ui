## 1. Tests

- [x] 1.1 `DictionaryViewConformanceTests.ASortedDictionary_ViewsAsDotNet`: a key the view finds after
  the dictionary gained it, beside the removal #919 measured; both are refused by the build on `main`

## 2. Fix

- [x] 2.1 Map `SortedDictionary<TKey, TValue>.KeyCollection.Contains` to the view's `contains`
