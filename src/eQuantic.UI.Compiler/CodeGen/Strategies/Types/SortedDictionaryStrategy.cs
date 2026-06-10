using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Owns the key-sorted dictionaries <c>SortedDictionary&lt;K, V&gt;</c> and <c>SortedList&lt;K, V&gt;</c>,
/// routing them to the runtime <c>$eq.collections.sortedDictionary</c>/<c>sortedList</c> so that
/// <c>Keys</c>/<c>Values</c>/<c>foreach</c> enumerate in key order (the defining .NET behavior). All the
/// dictionary routing — indexer, <c>ContainsKey</c>/<c>Add</c>/<c>Remove</c>/<c>Count</c>, etc. — is
/// inherited from <see cref="MapBackedDictionaryStrategy"/>.
/// </summary>
public class SortedDictionaryStrategy : MapBackedDictionaryStrategy
{
    protected override bool Matches(ITypeSymbol? type) => type.IsSortedDictionary();

    protected override string FactoryFor(ITypeSymbol? type) =>
        type.SortedDictionaryFactory() ?? Eq.SortedDictionary;
}
