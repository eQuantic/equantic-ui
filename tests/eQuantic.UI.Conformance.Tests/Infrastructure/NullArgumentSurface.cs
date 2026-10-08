using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// The BCL members eqc translates, each called with null for one of its reference parameters: the
/// probes <c>NullArgumentConformanceTests</c> runs on both sides (#569).
/// <para>
/// The surface is DERIVED, never listed. Its types are the ones the BCL surface audit's committed
/// record names (<c>bcl-surface.baseline.txt</c>, the coverage denominator); its members are what
/// reflection finds on them, constructors, indexers and generic methods included, which the audit's
/// probes cannot write; and which of them eqc translates is eqc's own answer, read from the converter's
/// diagnostics for each probe, as the audit reads its verdicts. The audit's line for a member is the
/// CLAIM that eqc translates it, and <see cref="AuditLines"/> is what lets the theory fail where a
/// claim with a reference parameter reaches no probe.
/// </para>
/// <para>
/// Each probe declares its arguments as typed locals and passes them by name, as a page passes a value
/// that happens to be null: a literal could take a path no variable takes. The other arguments are one
/// canonical value per type, and a receiver holds enough (three characters, three items) that an index
/// of 1 is in range, so what the probe measures is the null and not a range check before it.
/// </para>
/// </summary>
internal static class NullArgumentSurface
{
    /// <summary>One call with null for one parameter.</summary>
    /// <param name="Id">The member's id and the parameter's name, the baseline's key.</param>
    /// <param name="MemberId">As the audit writes a member: <c>String.Format(String,Object)</c>.</param>
    /// <param name="Parameter">The parameter handed null.</param>
    /// <param name="Statements">The C# both sides run: the arguments as locals, then the call.</param>
    /// <param name="Control">The same call with the parameter handed its canonical value instead, or
    /// null where its type has none. Where the control already answers otherwise on the two sides, the
    /// member itself differs, and what the null does says nothing more.</param>
    /// <param name="ComparesValue">Whether the member answers a value both sides print alike (a string,
    /// a bool, a char or a number), so a call that returns is compared by what it returned.</param>
    /// <param name="Target">The member the call must bind to, or the probe measures another.</param>
    internal sealed record Probe(string Id, string MemberId, string Parameter, string Statements, string? Control,
        bool ComparesValue, MethodBase Target);

    /// <summary>A member none of whose probes can be written, and why: an argument no canonical value
    /// speaks, or a parameter passed by reference.</summary>
    internal sealed record Unspoken(string MemberId, string Why);

    /// <summary>The namespaces a probe reads: the ones the audit's probe file opens, and the one the
    /// invariant culture a provider argument is handed lives in.</summary>
    internal static readonly string[] Namespaces =
        ["System", "System.Collections.Generic", "System.Globalization", "System.Linq", "System.Text", "System.Threading"];

    /// <summary>
    /// The assemblies a label resolves in: the ones that declare what <see cref="Namespaces"/> name for a
    /// probe, which are the ones the .NET side compiles it against.
    /// </summary>
    private static readonly Assembly[] Declaring =
    [
        typeof(object).Assembly,
        typeof(Stack<>).Assembly,
        typeof(Enumerable).Assembly,
    ];

    private static readonly Lazy<ILookup<string, Type>> Exported = new(() => Declaring
        .SelectMany(assembly => assembly.GetExportedTypes())
        .Where(type => type.Namespace is { } space && Namespaces.Contains(space) && !type.IsNested)
        .Distinct()
        .ToLookup(type => type.Name, StringComparer.Ordinal));

    // ---- The audit's record ---------------------------------------------------------------------

    /// <summary>One line of the audit's record: a verdict and the member it is about.</summary>
    internal sealed record AuditLine(string Verdict, string Id)
    {
        /// <summary>A strategy answered: <c>native</c> or <c>eq</c>.</summary>
        public bool Translated => Verdict is "native" or "eq";

        /// <summary>The label of the type the member is on, before the first dot.</summary>
        public string Owner => Id[..Id.IndexOf('.')];

        /// <summary>That type, resolved as the probe's C# resolves its label.</summary>
        public Type OwnerType => TypeOf(Owner) ?? throw new InvalidOperationException($"{Owner} resolves to no type");
    }

    internal static string AuditRecordPath() => Path.Combine(RepoRoot.Find()
            ?? throw new InvalidOperationException("repository root not found"),
        "tests", "eQuantic.UI.Compiler.Tests", "Coverage", "bcl-surface.baseline.txt");

    internal static IReadOnlyList<AuditLine> AuditLines() => File.ReadLines(AuditRecordPath())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .Select(line =>
        {
            var space = line.IndexOf(' ');
            return new AuditLine(line[..space], line[space..].Trim());
        })
        .ToList();

    /// <summary>
    /// The types the audit's record names, each resolved as the probe's C# would resolve its label. A
    /// label nothing resolves is a failure the caller reports: a type the audit gained and this
    /// derivation cannot read would otherwise drop out of the theory without a word.
    /// </summary>
    internal static (IReadOnlyList<Type> Owners, IReadOnlyList<string> Unresolved) Owners()
    {
        var owners = new List<Type>();
        var unresolved = new List<string>();
        foreach (var label in AuditLines().Select(line => line.Owner).Distinct(StringComparer.Ordinal))
        {
            if (TypeOf(label) is { } type) owners.Add(type);
            else unresolved.Add(label);
        }

        return (owners.Distinct().OrderBy(Label, StringComparer.Ordinal).ToList(), unresolved);
    }

    /// <summary>The C# keywords the audit's labels use for the two receivers it names by them.</summary>
    private static readonly Dictionary<string, Type> Keywords = new(StringComparer.Ordinal)
    {
        ["int"] = typeof(int), ["long"] = typeof(long), ["double"] = typeof(double),
        ["bool"] = typeof(bool), ["char"] = typeof(char), ["string"] = typeof(string),
        ["decimal"] = typeof(decimal), ["object"] = typeof(object),
    };

    /// <summary>A label (<c>List&lt;Int32&gt;</c>, <c>int[]</c>, <c>Nullable&lt;int&gt;</c>) as a type.</summary>
    internal static Type? TypeOf(string label)
    {
        if (label.EndsWith("[]", StringComparison.Ordinal)) return TypeOf(label[..^2])?.MakeArrayType();
        if (Keywords.TryGetValue(label, out var keyword)) return keyword;
        var open = label.IndexOf('<');
        if (open < 0) return Single(label);

        var arguments = SplitArguments(label[(open + 1)..^1]).Select(TypeOf).ToArray();
        if (arguments.Any(argument => argument is null)) return null;
        return Single($"{label[..open]}`{arguments.Length}")?.MakeGenericType(arguments!);

        static Type? Single(string name) => Exported.Value[name].ToList() is [var only] ? only : null;
    }

    private static IEnumerable<string> SplitArguments(string list)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < list.Length; i++)
        {
            if (list[i] == '<') depth++;
            else if (list[i] == '>') depth--;
            else if (list[i] == ',' && depth == 0)
            {
                yield return list[start..i].Trim();
                start = i + 1;
            }
        }

        yield return list[start..].Trim();
    }

    // ---- Labels ---------------------------------------------------------------------------------

    /// <summary>
    /// A type as the audit's record labels it (<c>BclSurfaceAuditTests.Label</c>): its simple name, a
    /// generic one with its arguments, and the two receivers it spells with keywords. A generic method's
    /// own parameters keep their names (<c>TSource</c>), as .NET's documentation writes them, and an
    /// argument that is generic itself is written whole, where the audit writes its bare name, which no
    /// member of its record needs and which made <c>IEnumerable&lt;int?&gt;</c> one id with
    /// <c>IEnumerable&lt;long?&gt;</c>.
    /// </summary>
    internal static string Label(Type type) => type switch
    {
        _ when type == typeof(int?) => "Nullable<int>",
        _ when type == typeof(int[]) => "int[]",
        _ when type.IsGenericType =>
            $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(Argument))}>",
        _ => type.Name,
    };

    private static string Argument(Type type) => type.IsGenericType ? Label(type) : type.Name;

    /// <summary>A member as the audit's record writes it: <c>String.Format(String,Object)</c>, a
    /// constructor as <c>String..ctor(Char[])</c>, an indexer by its accessor, <c>get_Item(String)</c>.</summary>
    internal static string MemberId(Type owner, MethodBase member)
    {
        var parameters = (member is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericMethodDefinition() : member)
            .GetParameters()
            .Select(parameter => Label(Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType));
        return $"{Label(owner)}.{member.Name}({string.Join(",", parameters)})";
    }

    /// <summary>
    /// The members a line of the audit's record names, found through the types its labels resolve to
    /// rather than by writing labels back: <c>String.Format(String,Object)</c> is <c>Format</c> on
    /// <c>string</c> with those parameters (a nullable one is written by its underlying type, as the
    /// audit writes it), and a LINQ line, <c>Enumerable.Select/1</c>, is every <c>Select</c> taking one
    /// argument besides its source. Empty for a property, a field or a method that takes nothing, and
    /// null for a line whose types do not resolve.
    /// </summary>
    internal static IReadOnlyList<MethodBase>? Claimed(AuditLine line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line.Id, @"^(?<owner>[^.]+)\.(?<name>[\w]+)(?:/(?<arity>\d+)|\((?<parameters>.*)\))?$");
        if (!match.Success || TypeOf(match.Groups["owner"].Value) is not { } owner) return null;
        var name = match.Groups["name"].Value;
        if (match.Groups["arity"].Success)
        {
            var count = int.Parse(match.Groups["arity"].Value, System.Globalization.CultureInfo.InvariantCulture) + 1;
            return Members(owner).Where(m => m.Name == name && m.GetParameters().Length == count).ToList();
        }

        if (!match.Groups["parameters"].Success || match.Groups["parameters"].Value.Length == 0) return [];
        var types = SplitArguments(match.Groups["parameters"].Value).Select(TypeOf).ToList();
        if (types.Any(type => type is null)) return null;
        return Members(owner).Where(m => m.Name == name && m.GetParameters()
                .Select(p => Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType)
                .SequenceEqual(types!))
            .ToList();
    }

    // ---- The members ----------------------------------------------------------------------------

    /// <summary>
    /// Every public member of an owner a page can call with an argument: its methods, static and
    /// instance (a generic one with every type argument <c>int</c>, the element the audit's receivers
    /// hold), its constructors and its indexers' accessors. What the audit's own probes leave out for
    /// reasons of their own (<c>CopyTo</c>) is in; what C# reaches by operator syntax (<c>op_*</c>), what
    /// takes nothing and what is obsolete are out.
    /// </summary>
    internal static IEnumerable<MethodBase> Members(Type owner)
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.DeclaredOnly;
        var methods = owner.GetMethods(Public | BindingFlags.Static)
            .Concat(owner.IsAbstract && !owner.IsSealed ? [] : owner.GetMethods(Public | BindingFlags.Instance))
            .Where(method => (!method.IsSpecialName || IsIndexerAccessor(method))
                && !method.Name.StartsWith("op_", StringComparison.Ordinal)
                && method.Name is not ("GetHashCode" or "GetType" or "GetTypeCode" or "GetEnumerator"
                    or "Deconstruct" or "GetPinnableReference" or "TryFormat")
                && method.GetCustomAttribute<ObsoleteAttribute>() is null
                && method.GetParameters().Length > 0)
            .Select(Instantiated)
            .OfType<MethodBase>();
        var constructors = owner.IsAbstract || owner.IsArray
            ? []
            : owner.GetConstructors().Where(constructor =>
                constructor.GetCustomAttribute<ObsoleteAttribute>() is null && constructor.GetParameters().Length > 0);
        return methods.Concat(constructors);

        static bool IsIndexerAccessor(MethodInfo method) =>
            method.Name is "get_Item" or "set_Item" && method.DeclaringType!.GetProperties()
                .Any(property => property.GetIndexParameters().Length > 0
                    && (property.GetMethod == method || property.SetMethod == method));
    }

    /// <summary>A generic method closed over <c>int</c>, or null where its constraints refuse it.</summary>
    private static MethodInfo? Instantiated(MethodInfo method)
    {
        if (!method.IsGenericMethodDefinition) return method;
        try
        {
            return method.MakeGenericMethod(method.GetGenericArguments().Select(_ => typeof(int)).ToArray());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>A parameter a null can be handed to: a reference type, passed by value.</summary>
    internal static bool TakesNull(ParameterInfo parameter) =>
        !parameter.ParameterType.IsValueType && !parameter.ParameterType.IsByRef && !parameter.ParameterType.IsPointer;

    // ---- The probes -----------------------------------------------------------------------------

    /// <summary>
    /// The probes of one member, one per parameter that takes null, and why any of them cannot be
    /// written: a probe needs every OTHER argument, so one target can be unspoken where its sibling is not.
    /// </summary>
    internal static (IReadOnlyList<Probe> Probes, IReadOnlyList<Unspoken> Unspoken) ProbesOf(Type owner, MethodBase member)
    {
        var memberId = MemberId(owner, member);
        var parameters = member.GetParameters();
        var nullable = parameters.Where(TakesNull).ToList();
        if (nullable.Count == 0) return ([], []);

        if (parameters.FirstOrDefault(p => p.ParameterType.IsByRef && !p.IsOut) is { } byRef)
            return ([], [new Unspoken(memberId, $"{byRef.Name} is passed by reference")]);

        var isExtension = member.IsDefined(typeof(ExtensionAttribute), inherit: false);
        var receiver = member.IsStatic || member is ConstructorInfo ? null : Receiver(owner);
        if (!member.IsStatic && member is MethodInfo && receiver is null)
            return ([], [new Unspoken(memberId, $"no receiver of {owner} can be written")]);

        var probes = new List<Probe>();
        var unspoken = new List<Unspoken>();
        foreach (var target in nullable)
        {
            if (ProbeOf(owner, member, memberId, parameters, target, receiver, isExtension) is { } probe) probes.Add(probe);
            else unspoken.Add(new Unspoken(memberId,
                $"no value of {parameters.First(p => p != target && !p.IsOut && Unwritable(p, isExtension, member, parameters)).ParameterType} can be written beside a null {target.Name}"));
        }

        return (probes, unspoken);
    }

    /// <summary>Whether no canonical value of a parameter can be written as an argument.</summary>
    private static bool Unwritable(ParameterInfo parameter, bool isExtension, MethodBase member, ParameterInfo[] parameters) =>
        (isExtension && parameter.Position == 0 ? Receiver(SourceType(parameter, member, parameters)) : Value(parameter.ParameterType)) is null;

    /// <summary>
    /// How the source of an extension is held: as the audit's receiver holds it, a List&lt;int&gt;, so
    /// the lowering measured is the one a page's <c>items.Where(…)</c> takes, save where List&lt;int&gt;
    /// has a method of that name of its own, which the call would bind instead.
    /// </summary>
    private static Type SourceType(ParameterInfo parameter, MethodBase member, ParameterInfo[] parameters) =>
        parameter.ParameterType.IsAssignableFrom(typeof(List<int>))
        && !typeof(List<int>).GetMethods().Any(m => m.Name == member.Name && m.GetParameters().Length == parameters.Length - 1)
            ? typeof(List<int>)
            : parameter.ParameterType;

    private static Probe? ProbeOf(Type owner, MethodBase member, string memberId, ParameterInfo[] parameters,
        ParameterInfo target, string? receiver, bool isExtension)
    {
        var statements = new StringBuilder();
        var control = new StringBuilder();
        if (receiver is not null)
        {
            statements.Append($"{CSharp(owner)} __receiver = {receiver}; ");
            control.Append($"{CSharp(owner)} __receiver = {receiver}; ");
        }

        var arguments = new List<string>();
        var controlled = true;
        foreach (var parameter in parameters)
        {
            var name = $"__a{parameter.Position}";
            if (parameter.IsOut)
            {
                arguments.Add($"out {CSharp(parameter.ParameterType.GetElementType()!)} {name}");
                continue;
            }

            var source = isExtension && parameter.Position == 0;
            var declared = source ? SourceType(parameter, member, parameters) : parameter.ParameterType;
            var value = source ? Receiver(declared) : Value(declared);
            if (parameter == target)
            {
                statements.Append($"{CSharp(declared)} {name} = null; ");
                if (value is null) controlled = false;
                else control.Append($"{CSharp(declared)} {name} = {value}; ");
            }
            else if (value is null)
            {
                return null;
            }
            else if (WrittenAtTheCall(declared))
            {
                arguments.Add(value);
                continue;
            }
            else
            {
                statements.Append($"{CSharp(declared)} {name} = {value}; ");
                control.Append($"{CSharp(declared)} {name} = {value}; ");
            }

            arguments.Add(name);
        }

        var call = Call(owner, member, receiver is not null, isExtension, arguments);
        return new Probe($"{memberId} {target.Name}", memberId, target.Name!, statements.Append(call).ToString(),
            controlled ? control.Append(call).ToString() : null, ComparesValue(member), member);
    }

    /// <summary>
    /// A culture is handed where the call names it, as a page writes <c>int.Parse(s,
    /// CultureInfo.InvariantCulture)</c>: eqc reads the culture a call is given from its spelling
    /// there (<c>NamedCulture</c>), and one held in a local reaches the browser as the name
    /// <c>CultureInfo</c>, which the runtime does not export, a defect of its own that every
    /// provider overload would otherwise report in place of its null.
    /// </summary>
    private static bool WrittenAtTheCall(Type type) => type == typeof(IFormatProvider) || type == typeof(CultureInfo);

    /// <summary>The call, returning what the member answers, or null for a member that answers nothing.</summary>
    private static string Call(Type owner, MethodBase member, bool onReceiver, bool isExtension, List<string> arguments)
    {
        if (member is ConstructorInfo) return $"return new {CSharp(owner)}({string.Join(", ", arguments)});";

        var method = (MethodInfo)member;
        if (method.Name == "get_Item") return $"return __receiver[{string.Join(", ", arguments)}];";
        if (method.Name == "set_Item")
            return $"__receiver[{string.Join(", ", arguments.Take(arguments.Count - 1))}] = {arguments[^1]}; return null;";

        var typeArguments = method.IsGenericMethod && !Inferable(method.GetGenericMethodDefinition())
            ? $"<{string.Join(", ", method.GetGenericArguments().Select(CSharp))}>"
            : "";
        var call = isExtension
            ? $"{arguments[0]}.{method.Name}{typeArguments}({string.Join(", ", arguments.Skip(1))})"
            : $"{(onReceiver ? "__receiver" : CSharp(owner))}.{method.Name}{typeArguments}({string.Join(", ", arguments)})";
        return method.ReturnType == typeof(void) ? $"{call}; return null;" : $"return {call};";
    }

    /// <summary>Whether C# infers every type argument of a generic method from its parameters.</summary>
    private static bool Inferable(MethodInfo definition) =>
        definition.GetGenericArguments().All(argument =>
            definition.GetParameters().Any(parameter => Mentions(parameter.ParameterType, argument)));

    private static bool Mentions(Type type, Type argument) =>
        type == argument
        || (type.HasElementType && Mentions(type.GetElementType()!, argument))
        || (type.IsGenericType && type.GetGenericArguments().Any(inner => Mentions(inner, argument)));

    /// <summary>
    /// A member whose answer both sides print alike: a string, a bool, a char or a number. A collection,
    /// a builder or a lazy sequence prints as each side holds it, so for those only the throw is compared.
    /// </summary>
    private static bool ComparesValue(MethodBase member) =>
        (member is ConstructorInfo constructor ? constructor.DeclaringType : ((MethodInfo)member).ReturnType) is { } answer
        && (Nullable.GetUnderlyingType(answer) ?? answer) is var plain
        && (plain == typeof(string) || plain == typeof(bool) || plain == typeof(char) || plain == typeof(decimal)
            || (plain.IsPrimitive && plain != typeof(IntPtr) && plain != typeof(UIntPtr)));

    // ---- Values ---------------------------------------------------------------------------------

    /// <summary>
    /// What a receiver of the owner holds: enough that an index and a count of 2 are in range, so a null
    /// is what the call meets first.
    /// </summary>
    internal static string? Receiver(Type owner)
    {
        if (owner == typeof(string)) return "\"abcd\"";
        if (owner == typeof(StringBuilder)) return "new StringBuilder(\"abcd\")";
        if (owner.IsGenericType && owner.GetGenericTypeDefinition() is var definition
            && (definition == typeof(List<>) || definition == typeof(HashSet<>) || definition == typeof(SortedSet<>))
            && owner.GetGenericArguments()[0] == typeof(int))
            return $"new {CSharp(owner)} {{ 1, 2, 3, 4 }}";
        return Value(owner);
    }

    /// <summary>
    /// One canonical, non-null value of a type, as C#, or null where none can be written. A number is
    /// 2, the smallest that is a valid base (<c>Convert.ToInt32(s, 2)</c>), a string holds three
    /// characters and a sequence four items, so an index of 2 and a count of 2 are in range of them.
    /// </summary>
    internal static string? Value(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying) return Value(underlying);
        if (type == typeof(int)) return "2";
        if (type == typeof(uint)) return "2u";
        if (type == typeof(long)) return "2L";
        if (type == typeof(ulong)) return "2UL";
        if (type == typeof(short)) return "(short)2";
        if (type == typeof(ushort)) return "(ushort)2";
        if (type == typeof(byte)) return "(byte)2";
        if (type == typeof(sbyte)) return "(sbyte)2";
        if (type == typeof(double)) return "1.5";
        if (type == typeof(float)) return "1.5f";
        if (type == typeof(decimal)) return "1.5m";
        if (type == typeof(bool)) return "true";
        if (type == typeof(char)) return "'a'";
        if (type == typeof(string)) return "\"abc\"";
        if (type == typeof(object)) return "\"abc\"";
        if (type == typeof(StringComparison)) return "StringComparison.Ordinal";
        if (type.IsEnum) return $"{CSharp(type)}.{Enum.GetName(type, Enum.GetValues(type).GetValue(0)!)}";
        if (type == typeof(Guid)) return "Guid.Parse(\"0f8fad5b-d9cb-469f-a165-70867728950e\")";
        if (type == typeof(DateTime)) return "new DateTime(2026, 1, 2)";
        if (type == typeof(TimeSpan)) return "TimeSpan.FromMinutes(90)";
        if (type == typeof(DateOnly)) return "new DateOnly(2026, 1, 2)";
        if (type == typeof(TimeOnly)) return "new TimeOnly(10, 30)";
        if (type == typeof(DateTimeOffset)) return "new DateTimeOffset(new DateTime(2026, 1, 2), TimeSpan.Zero)";
        // The one culture eqc lets cross for reading and writing a number or a date (EQ2108).
        if (type == typeof(IFormatProvider) || type == typeof(CultureInfo)) return "CultureInfo.InvariantCulture";
        if (type == typeof(CancellationToken)) return "CancellationToken.None";
        if (type == typeof(CancellationTokenSource)) return "new CancellationTokenSource()";
        if (type == typeof(StringBuilder)) return "new StringBuilder(\"abc\")";
        if (type == typeof(Array)) return "new int[] { 2, 2, 2, 2 }";
        if (type == typeof(System.Collections.IEnumerable)) return "new List<int> { 2, 2, 2, 2 }";
        if (type.IsArray && type.GetArrayRank() == 1 && Value(type.GetElementType()!) is { } element)
            return $"new {CSharp(type.GetElementType()!)}[] {{ {element}, {element}, {element}, {element} }}";
        if (typeof(Delegate).IsAssignableFrom(type)) return Lambda(type);
        if (!type.IsGenericType) return null;

        var definition = type.GetGenericTypeDefinition();
        var arguments = type.GetGenericArguments();
        if (definition == typeof(KeyValuePair<,>) && Value(arguments[0]) is { } key && Value(arguments[1]) is { } pair)
            return $"new {CSharp(type)}({key}, {pair})";
        if (definition == typeof(IOrderedEnumerable<>) && Value(arguments[0]) is { } ordered)
            return $"new List<{CSharp(arguments[0])}> {{ {ordered} }}.OrderBy(__o => __o)";
        if ((definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>)
                || definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(SortedDictionary<,>))
            && Value(arguments[0]) is { } k && Value(arguments[1]) is { } v)
            return $"new {(definition == typeof(SortedDictionary<,>) ? "SortedDictionary" : "Dictionary")}<{CSharp(arguments[0])}, {CSharp(arguments[1])}> {{ [{k}] = {v} }}";
        if ((definition == typeof(HashSet<>) || definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>)
                || definition == typeof(SortedSet<>))
            && Value(arguments[0]) is { } member)
            return $"new {(definition == typeof(SortedSet<>) ? "SortedSet" : "HashSet")}<{CSharp(arguments[0])}> {{ {member} }}";
        if ((definition == typeof(List<>) || definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>)
                || definition == typeof(IList<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(IReadOnlyList<>))
            && Value(arguments[0]) is { } item)
            return $"new List<{CSharp(arguments[0])}> {{ {item}, {item}, {item}, {item} }}";
        if ((definition == typeof(Queue<>) || definition == typeof(Stack<>)) && Value(arguments[0]) is not null)
            return $"new {CSharp(type)}()";
        return null;
    }

    /// <summary>A lambda of a delegate type that answers a canonical value, or does nothing.</summary>
    private static string? Lambda(Type type)
    {
        var invoke = type.GetMethod("Invoke");
        if (invoke is null || invoke.GetParameters().Any(p => p.ParameterType.IsByRef)) return null;
        var parameters = string.Join(", ", invoke.GetParameters().Select(p => $"__l{p.Position}"));
        if (invoke.ReturnType == typeof(void)) return $"({parameters}) => {{ }}";
        return Value(invoke.ReturnType) is { } answer ? $"({parameters}) => {answer}" : null;
    }

    private static readonly Dictionary<Type, string> Aliases = new()
    {
        [typeof(int)] = "int", [typeof(uint)] = "uint", [typeof(long)] = "long", [typeof(ulong)] = "ulong",
        [typeof(short)] = "short", [typeof(ushort)] = "ushort", [typeof(byte)] = "byte", [typeof(sbyte)] = "sbyte",
        [typeof(double)] = "double", [typeof(float)] = "float", [typeof(decimal)] = "decimal",
        [typeof(bool)] = "bool", [typeof(char)] = "char", [typeof(string)] = "string", [typeof(object)] = "object",
    };

    /// <summary>A type as C# writes it in a probe, qualified where the probe's namespaces do not reach it.</summary>
    internal static string CSharp(Type type)
    {
        if (Aliases.TryGetValue(type, out var alias)) return alias;
        if (Nullable.GetUnderlyingType(type) is { } underlying) return CSharp(underlying) + "?";
        if (type.IsArray) return CSharp(type.GetElementType()!) + "[]";
        var name = type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] : type.Name;
        if (type.IsGenericType) name += $"<{string.Join(", ", type.GetGenericArguments().Select(CSharp))}>";
        if (type.IsNested) return $"{CSharp(type.DeclaringType!)}.{name}";
        return type.Namespace is { } space && Namespaces.Contains(space) ? name : $"global::{type.Namespace}.{name}";
    }
}
