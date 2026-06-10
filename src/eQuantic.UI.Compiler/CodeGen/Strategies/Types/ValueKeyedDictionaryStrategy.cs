using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Owns <c>Dictionary&lt;TKey, TValue&gt;</c> whose KEY is a structural value type (record / <c>struct</c>
/// / value tuple). Such a key cannot index a plain JS object — the object coerces it to a string via
/// <c>toString</c>, collapsing distinct values — so it routes to the runtime
/// <c>$eq.collections.valueMap</c>, which keys by structural equality (<c>$eq.equals</c>).
/// String/number/enum-keyed dictionaries are left entirely to the plain-object dictionary strategy.
/// All routing is inherited from <see cref="MapBackedDictionaryStrategy"/>.
/// </summary>
public class ValueKeyedDictionaryStrategy : MapBackedDictionaryStrategy
{
    protected override bool Matches(ITypeSymbol? type) => type.IsValueKeyedDictionary();

    protected override string FactoryFor(ITypeSymbol? type) => Eq.ValueMap;
}
