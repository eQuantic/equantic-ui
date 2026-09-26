using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A default interface member reaches the twin of every class that relies on it (#414), whichever
/// emitter writes the class: a plain class, as <c>PlainTextLanguage</c> is, and a component. The
/// record and struct path is executed on both sides by <c>DefaultInterfaceMemberConformanceTests</c>.
/// </summary>
public class DefaultInterfaceMemberEmissionTests
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["IStyled.cs"] = """
            namespace App;
            public interface IStyled
            {
                string Name { get; }
                Spacing Gaps => new Spacing(4);
                string Caption() => Name + "!";
            }
            """,
        // Spacing is named only by the interface's default, never by a class that relies on it.
        ["Spacing.cs"] = "namespace App; public sealed record Spacing(int Width);",
        ["Plain.cs"] = """
            namespace App;
            public sealed class Plain : IStyled
            {
                public string Name => "plain";
            }
            """,
        ["Custom.cs"] = """
            namespace App;
            public sealed class Custom : IStyled
            {
                public string Name => "custom";
                public Spacing Gaps => new Spacing(8);
            }
            """,
        ["Badge.cs"] = """
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Badge : StatelessComponent, IStyled
            {
                public string Name => "badge";
                public override VisualNode Build(ComponentContext context) => new Text(((IStyled)this).Caption());
            }
            """,
        // An app's own language, against the code engine's ASSEMBLY: ICodeLanguage is metadata here,
        // as it is in every app, and the runtime carries its defaults.
        ["Words.cs"] = """
            using System.Collections.Generic;
            using eQuantic.UI.Code;
            namespace App;
            public sealed class Words : ICodeLanguage
            {
                public string Name => "words";
                public int Tokenize(string line, int state, List<CodeToken> into) => state;
            }
            """,
        // Two defaults that lower to one name: the twin holds one member per name.
        ["Doubled.cs"] = """
            namespace App;
            public interface IAlpha { string Mark() => "a"; }
            public interface IBeta { string Mark() => "b"; }
            public sealed class Doubled : IAlpha, IBeta { public int N; }
            """,
        // A default that reaches a static of its interface, which has no JavaScript home.
        ["Wrapped.cs"] = """
            namespace App;
            public interface IWrapping
            {
                string Show() => Wrap("x");
                private static string Wrap(string text) => "<" + text + ">";
            }
            public sealed class Wrapped : IWrapping { public int N; }
            """,
        // A type named only in a default method's signature.
        ["Canvas.cs"] = """
            namespace App;
            public interface IDraw { string Draw(Spacing gap) => "drawn"; }
            public sealed class Canvas : IDraw { public int N; }
            """,
        // A field on the name of a default: the twin would hold two members named `mark`.
        ["FieldClash.cs"] = """
            namespace App;
            public interface IFielded { string Mark() => "m"; }
            public sealed class FieldClash : IFielded { public int Mark; }
            """,
        // A private helper no default calls, on a name the class uses: neither copied nor a clash.
        ["Quiet.cs"] = """
            namespace App;
            public interface IHidden
            {
                string Show() => "shown";
                private string Hidden() => "helper";
            }
            public sealed class Quiet : IHidden { public int Hidden; }
            """,
        // A type named only inside a generic argument of a default's parameter.
        ["Converter.cs"] = """
            namespace App;
            public interface IConvert { int Convert(System.Func<Spacing, int> measure) => 0; }
            public sealed class Converter : IConvert { public int N; }
            """,
        // Along the chain (found in review, #418): a derived field on the name of a default its base takes.
        ["ChainDerived.cs"] = """
            namespace App;
            public interface IChained { string Mark() => "m"; }
            public class ChainBase : IChained { public int N; }
            public sealed class ChainDerived : ChainBase { public int Mark; }
            """,
        // A derived member that implements the default's interface member, because it lists the interface again.
        ["Relisted.cs"] = """
            namespace App;
            public sealed class Relisted : ChainBase, IChained { public string Mark() => "r"; }
            """,
        // A default on the name of a member a base declares.
        ["Labelled.cs"] = """
            namespace App;
            public class Holder { public int Label; }
            public interface ILabel { string Label() => "l"; }
            public sealed class Labelled : Holder, ILabel { public int Own; }
            """,
        // Two defaults of different interface members, one taken by the base and one by the derived class.
        ["SecondDerived.cs"] = """
            namespace App;
            public interface IFirst { string Tag() => "1"; }
            public interface ISecond { string Tag() => "2"; }
            public class FirstBase : IFirst { public int N; }
            public sealed class SecondDerived : FirstBase, ISecond { public int Own; }
            """,
        // A derived class that lists an interface overriding its base's default takes the more specific one.
        ["LoudSpeaker.cs"] = """
            namespace App;
            public interface ISpeaker { string Speak() => "soft"; }
            public interface ILoudSpeaker : ISpeaker { string ISpeaker.Speak() => "LOUD"; }
            public class SoftSpeaker : ISpeaker { public int N; }
            public sealed class LoudSpeaker : SoftSpeaker, ILoudSpeaker { public int Own; }
            """,
        // A derived field on the name of a helper its base takes with the default that calls it.
        ["HelpedDerived.cs"] = """
            namespace App;
            public interface IHelped
            {
                string Show() => Wrap("x");
                private string Wrap(string text) => "<" + text + ">";
            }
            public class HelpedBase : IHelped { public int N; }
            public sealed class HelpedDerived : HelpedBase { public int Wrap; }
            """,
        // A class over the foreign interface above, declared in the vocabulary's namespace.
        ["Foreigner.cs"] = """
            namespace App;
            public sealed class Foreigner : eQuantic.UI.Code.IForeignLanguage { public int N; }
            """,
        // A default indexer: a twin has no form for one (#427).
        ["Indexed.cs"] = """
            namespace App;
            public interface IIndexed { int this[int i] => i * 2; }
            public sealed class Indexed : IIndexed { public int N; }
            """,
        // An explicit implementation of one interface beside another's default of the same name.
        ["Twice.cs"] = """
            namespace App;
            public interface IOne { string M(); }
            public interface ITwo { string M() => "two"; }
            public sealed class Twice : IOne, ITwo { string IOne.M() => "one"; }
            """,
        // A static of the interface reached through a private helper, not by the default itself.
        ["Deep.cs"] = """
            namespace App;
            public interface IDeep
            {
                string Show() => Wrap("x");
                private string Wrap(string text) => Bracket(text);
                private static string Bracket(string text) => "<" + text + ">";
            }
            public sealed class Deep : IDeep { public int N; }
            """,
        // An event on the name of a default: an event lowers to an instance field.
        ["Eventful.cs"] = """
            namespace App;
            public interface INotify { string Changed() => "changed"; }
            public sealed class Eventful : INotify { public event System.Action Changed; }
            """,
        // A primary constructor's parameter on the name of a default: the twin holds it as a field.
        ["Captured.cs"] = """
            namespace App;
            public interface IMarkC { string Mark() => "m"; }
            public sealed class Captured(int mark) : IMarkC { public int Twice() => mark * 2; }
            """,
        // And a positional record's parameter, which is a property.
        ["Positional.cs"] = """
            namespace App;
            public interface IMarkP { string Mark() => "m"; }
            public sealed record Positional(int Mark) : IMarkP;
            """,
        // A default that names a helper and a static only in nameof, which runs neither. Properties,
        // since a method's name in nameof binds a method group and no one symbol.
        ["Named.cs"] = """
            namespace App;
            public interface INamed
            {
                string Show() => nameof(Hidden) + nameof(Stat);
                private string Hidden => "helper";
                private static string Stat => "static";
            }
            public sealed class Named : INamed { public int Hidden; }
            """,
        // A component's server-only method on a default's name: it never reaches the twin.
        ["Served.cs"] = """
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;
            namespace App;
            public interface IShown { string Show() => "default"; }
            public sealed class Served : StatelessComponent, IShown
            {
                [ServerOnly] public string Show(int id) => "server " + id;
                public override VisualNode Build(ComponentContext context) => new Text(((IShown)this).Show());
            }
            """,
        // Compiled into a referenced assembly below, not into this compilation.
        ["Voiced.cs"] = """
            namespace App;
            public sealed class Voiced : Lib.IVoice
            {
                public string Word => "hello";
            }
            """,
    };

    private const string Library = """
        namespace Lib
        {
            public interface IVoice
            {
                string Word { get; }
                string Say() => Word + ".";
            }
        }

        // Another assembly's interface in the code engine's namespace: the runtime carries none of
        // its defaults, whatever its namespace says.
        namespace eQuantic.UI.Code
        {
            public interface IForeignLanguage
            {
                string Greet() => "hi";
            }
        }
        """;

    /// <summary>Compiled the way eqc compiles an app, with <see cref="Library"/> as a referenced
    /// assembly: its interface has no syntax in the app's compilation, only metadata.</summary>
    private static CompilationResult Compile(string component, bool succeeds = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-default-members-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToList();
            var library = CSharpCompilation.Create("Lib", [CSharpSyntaxTree.ParseText(Library)], platform,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var image = new MemoryStream();
            library.Emit(image).Success.Should().BeTrue();

            var trees = Files.Select(file =>
            {
                var path = Path.Combine(dir, file.Key);
                File.WriteAllText(path, file.Value);
                return CSharpSyntaxTree.ParseText(file.Value, path: path);
            }).ToList();
            var references = platform
                .Append(MetadataReference.CreateFromImage(image.ToArray()))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Components.CodeEditor).Assembly.Location))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Code.ICodeLanguage).Assembly.Location));
            var compilation = CSharpCompilation.Create("App", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var result = compiler.CompileFile(Path.Combine(dir, component + ".cs"))
                .Single(r => r.ComponentName == component);
            if (succeeds) result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
            return result;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void APlainClassTakesTheDefaultsItDoesNotDeclare()
    {
        var ts = Compile("Plain").TypeScript;

        ts.Should().MatchRegex(@"get gaps\(\)(: Spacing)? \{\s*return new Spacing\(4\);");
        // The default reads the class's own member through `this`, as the interface's `Name` does.
        ts.Should().MatchRegex(@"caption\(\)(: string)? \{\s*return \(this\.name \?\? ''\) \+ '!';");
        // Named only by the default: the scan that decides the imports has to read it too.
        ts.Should().Contain("import { Spacing } from \"./Spacing\"");
    }

    [Fact]
    public void AMemberTheClassDeclaresIsNotWrittenTwice()
    {
        var ts = Compile("Custom").TypeScript;

        ts.Should().Contain("new Spacing(8)");
        ts.Should().NotContain("new Spacing(4)");
        ts.Should().MatchRegex(@"caption\(\)");
    }

    [Fact]
    public void AComponentTakesTheDefaultsItDoesNotDeclare()
    {
        var ts = Compile("Badge").TypeScript;

        ts.Should().MatchRegex(@"caption\(\)(: string)? \{\s*return");
        ts.Should().MatchRegex(@"get gaps\(\)");
    }

    /// <summary>
    /// A vocabulary default reaches an APP's class as a delegation to the runtime's copy: the app
    /// compiles against the vocabulary's assembly, where the interface has no body to convert. An
    /// app's language without its own Rules is the #414 crash moved from the SDK's class to the
    /// app's, and a themed app took three warnings on every build (measured on the site).
    /// </summary>
    [Fact]
    public void AVocabularyDefaultIsDelegatedToTheRuntime()
    {
        var result = Compile("Words");

        result.TypeScript.Should().MatchRegex(@"get rules\(\)(: \w+)? \{\s*return ICodeLanguage\.rules\(this\);");
        result.TypeScript.Should().MatchRegex(@"import \{[^}]*\bICodeLanguage\b[^}]*\} from ""@equantic/runtime""");
        result.Errors.Should().NotContain(error => error.Code == "EQ1008");
    }

    /// <summary>Two defaults that lower to one name (found in review, #418): C# reaches each through
    /// its interface, and the twin would keep one of them.</summary>
    [Fact]
    public void TwoDefaultsOnOneNameAreRefused()
    {
        var result = Compile("Doubled", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'Doubled' takes the default 'IBeta.Mark'")
            .And.Contain("'IAlpha.Mark'");
    }

    /// <summary>A default that reaches a static member of its interface (found in review, #418): an
    /// interface has no JavaScript form, so nothing would define the static it calls.</summary>
    [Fact]
    public void ADefaultReachingAnInterfaceStaticIsRefused()
    {
        var result = Compile("Wrapped", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1008")
            .Which.Message.Should().Contain("IWrapping.Wrap, a static member of an interface")
            .And.Contain("Declare Show in Wrapped");
    }

    /// <summary>An event on the name of a default (found in review, #418): the event is an instance
    /// field, which shadows the default on the prototype for every call through the interface.</summary>
    [Fact]
    public void AnEventOnADefaultsNameIsRefused()
    {
        var result = Compile("Eventful", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'Eventful' takes the default 'INotify.Changed', which lowers to `changed`, "
                + "and so does 'Eventful.Changed'");
    }

    /// <summary>A primary constructor's parameter on the name of a default (found in review, #418): the
    /// twin assigns every such parameter to a field, which shadows the default on the prototype.</summary>
    [Fact]
    public void APrimaryConstructorParameterOnADefaultsNameIsRefused()
    {
        var result = Compile("Captured", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'Captured' takes the default 'IMarkC.Mark', which lowers to `mark`, and so does "
                + "'Captured(mark)', a primary constructor's parameter the twin holds as a field");
    }

    /// <summary>A positional record's parameter is a property, refused as one, and once.</summary>
    [Fact]
    public void APositionalParameterOnADefaultsNameIsRefusedOnce()
    {
        Compile("Positional", succeeds: false).Errors.Should().ContainSingle(error => error.Code == "EQ1007");
    }

    /// <summary>A name inside <c>nameof</c> is text, not a call (found in review, #418): the helper it
    /// names is not copied, a class field on its name is no clash, and a static named there is not
    /// one the default reaches.</summary>
    [Fact]
    public void ANameInsideNameofIsNotACall()
    {
        var result = Compile("Named");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007" || error.Code == "EQ1008");
        result.TypeScript.Should().NotContain("'helper'").And.NotContain("'static'");
    }

    /// <summary>A component's server-only method takes no name in its twin, so a default on that name
    /// is no clash (asked in review, #418).</summary>
    [Fact]
    public void AComponentsServerOnlyMethodIsNoClash()
    {
        var result = Compile("Served");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
        result.TypeScript.Should().Contain("'default'").And.NotContain("'server '");
    }

    /// <summary>A static of the interface reached through a private helper is refused too (asked in
    /// review, #418): every helper a default carries is checked as the default is, so the helper
    /// that reaches the static is the one the error names.</summary>
    [Fact]
    public void AStaticReachedThroughAHelperIsRefused()
    {
        var result = Compile("Deep", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1008")
            .Which.Message.Should().Contain("Deep relies on the default IDeep.Wrap, which uses IDeep.Bracket");
    }

    /// <summary>A type named only in a default method's signature is imported, since the signature
    /// annotates it (found in review, #418).</summary>
    [Fact]
    public void ATypeInADefaultsSignatureIsImported()
    {
        var ts = Compile("Canvas").TypeScript;

        ts.Should().MatchRegex(@"draw\(_?gap(: Spacing)?\)");
        ts.Should().Contain("import { Spacing } from \"./Spacing\"");
    }

    /// <summary>A field on the name of a default (found in review, #418): the twin holds one member
    /// per name, fields included.</summary>
    [Fact]
    public void AFieldOnADefaultsNameIsRefused()
    {
        var result = Compile("FieldClash", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'FieldClash' takes the default 'IFielded.Mark'");
    }

    /// <summary>A private helper no default calls travels nowhere (found in review, #418): it is not
    /// written into the twin, and a class member on its name is no clash.</summary>
    [Fact]
    public void AnUncalledHelperIsNotCopied()
    {
        var result = Compile("Quiet");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
        result.TypeScript.Should().Contain("'shown'");
        result.TypeScript.Should().NotContain("'helper'");
    }

    /// <summary>A type named only in a generic argument of a default's parameter is imported (found in
    /// review, #418): a scan that kept a generic's last argument read `int` from `Func&lt;Spacing, int&gt;`.</summary>
    [Fact]
    public void ATypeInsideAGenericArgumentIsImported()
    {
        var ts = Compile("Converter").TypeScript;

        ts.Should().Contain("import { Spacing } from \"./Spacing\"");
    }

    /// <summary>A derived member on the name of a default its base takes (found in review, #418): the
    /// base's twin holds the default, and the derived field shadowed it for every call through the
    /// interface.</summary>
    [Fact]
    public void ADerivedMemberOnABasesDefaultIsRefused()
    {
        var result = Compile("ChainDerived", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'ChainDerived.Mark' lowers to `mark`, and so does the default "
                + "'IChained.Mark', which it inherits from 'ChainBase'");
    }

    /// <summary>A derived member that implements the default's interface member, because the class lists
    /// the interface again, is what the interface reaches for it: over the base's default is right.</summary>
    [Fact]
    public void ADerivedMemberThatReimplementsTheInterfaceIsNoClash()
    {
        var result = Compile("Relisted");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
        result.TypeScript.Should().MatchRegex(@"mark\(\)(: string)? \{\s*return 'r';");
    }

    /// <summary>A default on the name of a member a base declares (found in review, #418): the base's
    /// field is set on the instance, over the default the derived prototype holds.</summary>
    [Fact]
    public void ADefaultOnABasesMemberIsRefused()
    {
        var result = Compile("Labelled", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'Labelled' takes the default 'ILabel.Label', which lowers to `label`, "
                + "and so does 'Holder.Label', which it inherits");
    }

    /// <summary>Two defaults of different interface members along the chain (found in review, #418): the
    /// derived class's would answer the calls through its base's interface.</summary>
    [Fact]
    public void TwoDefaultsAlongTheChainAreRefused()
    {
        var result = Compile("SecondDerived", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'SecondDerived' takes the default 'ISecond.Tag', which lowers to `tag`, "
                + "and so does the default 'IFirst.Tag', which it inherits from 'FirstBase'");
    }

    /// <summary>A derived class that lists an interface overriding its base's default takes the more
    /// specific default (found in review, #418): skipping every interface the base implements left it
    /// out of the derived twin, which answered with the base's. Executed on both sides by
    /// <c>DefaultInterfaceMemberConformanceTests</c>.</summary>
    [Fact]
    public void AMoreSpecificDefaultReachesTheDerivedTwin()
    {
        var result = Compile("LoudSpeaker");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
        result.TypeScript.Should().MatchRegex(@"speak\(\)(: string)? \{\s*return 'LOUD';");
    }

    /// <summary>A derived member on the name of a private helper its base takes with the default that
    /// calls it (found in review, #418): the default's call would reach the member.</summary>
    [Fact]
    public void ADerivedMemberOnABasesHelperIsRefused()
    {
        var result = Compile("HelpedDerived", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'HelpedDerived.Wrap' lowers to `wrap`, and so does 'IHelped.Wrap', "
                + "which it inherits from 'HelpedBase' with the defaults that call it");
    }

    /// <summary>An interface another assembly declares in the vocabulary's namespace is not one the
    /// runtime carries (found in review, #418): delegating to it named an export the runtime does not
    /// have, where the build had to refuse the class.</summary>
    [Fact]
    public void AForeignInterfaceInTheVocabularysNamespaceIsNotDelegated()
    {
        var result = Compile("Foreigner", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1008")
            .Which.Message.Should().Contain("Foreigner relies on the default IForeignLanguage.Greet");
        (result.TypeScript ?? "").Should().NotContain("IForeignLanguage.greet(this)");
    }

    /// <summary>A default indexer is refused by name (found in review, #418): it fell to the message for
    /// a body compiled into another assembly, which it is not, and no twin has an indexer yet (#427).</summary>
    [Fact]
    public void ADefaultIndexerIsRefusedForWhatItIs()
    {
        var result = Compile("Indexed", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1008")
            .Which.Message.Should().Contain("Indexed relies on the default indexer of IIndexed")
            .And.Contain("#427")
            .And.NotContain("compiled into");
    }

    /// <summary>An explicit implementation lowers under its member's own name (found in review, #418):
    /// `IOne.M` and a default `ITwo.M` gave the twin two members named `m`.</summary>
    [Fact]
    public void AnExplicitImplementationOnADefaultsNameIsRefused()
    {
        var result = Compile("Twice", succeeds: false);

        result.Errors.Should().ContainSingle(error => error.Code == "EQ1007")
            .Which.Message.Should().Contain("'Twice' takes the default 'ITwo.M', which lowers to `m`, and so does 'Twice.IOne.M'");
    }

    /// <summary>A default whose body is compiled into a referenced assembly the runtime does not
    /// carry cannot be written, and the build refuses the class instead of emitting a twin that
    /// answers undefined, naming the two ways out.</summary>
    [Fact]
    public void ADefaultFromAReferencedAssemblyIsRefused()
    {
        var result = Compile("Voiced", succeeds: false);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.Code == "EQ1008")
            .Which.Message.Should().Contain("Voiced relies on the default IVoice.Say")
            .And.Contain("Declare Say in Voiced, or keep Voiced out of client code");
    }
}
