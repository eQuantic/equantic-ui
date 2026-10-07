namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The vocabulary's public types by the name their runtime twin is declared under: a generic type's
/// without its arity, since <c>ServerTopic&lt;T&gt;</c> is <c>ServerTopic`1</c> to reflection and
/// <c>ServerTopic</c> in TypeScript, where type arguments are erased. The guards that pair each
/// hand-written twin with the C# it mirrors read it; paired by the CLR name, every generic twin was
/// skipped in silence.
/// </summary>
internal static class VocabularyTypesByTwinName
{
    public static Dictionary<string, Type> Get() =>
        typeof(Primitives.VisualNode).Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .GroupBy(TwinName)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static string TwinName(Type type)
    {
        var arity = type.Name.IndexOf('`');
        return arity < 0 ? type.Name : type.Name[..arity];
    }
}
