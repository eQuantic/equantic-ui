using System;
using System.IO;
using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Regression tests graduated from the authoring-coverage sweep: a developer can declare component
/// properties (computed, get/set, static, auto with defaults) and constructors with bodies, and reference
/// primitive types, without the transpiler dropping members or inventing bogus imports.
/// </summary>
public class AuthoringCoverageTests
{
    private const string Header =
        "using System; using System.Collections.Generic; using eQuantic.UI.Core; using eQuantic.UI.Web.Components; namespace App; ";

    private static string Ts(string body) =>
        new ComponentCompiler().CompileSource(Header + body).Single(r => r.ComponentName == "C").TypeScript;

    private static string TsOf(string name, string body) =>
        new ComponentCompiler().CompileSource(Header + body).Single(r => r.ComponentName == name).TypeScript;

    /// <summary>
    /// Same, but with the dependency resolver the real build wires — it is what tells a module which
    /// names are app modules it may import. Static-helper modules emit NO per-app import without it.
    /// </summary>
    private static string TsOfResolved(string name, string body)
    {
        var source = Header + body;
        var dir = Path.Combine(Path.GetTempPath(), "eq-authoring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Source.cs"), source);
            var resolver = new eQuantic.UI.Compiler.Services.ComponentDependencyResolver();
            resolver.ScanSourceDirectories(new[] { dir });
            var compiler = new ComponentCompiler();
            compiler.SetDependencyResolver(resolver);
            return compiler.CompileSource(source).Single(r => r.ComponentName == name).TypeScript;
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ComputedGetOnlyProperty_EmitsGetter()
    {
        var ts = Ts("public class C : StatelessComponent { public int N { get; set; } public int Double => N * 2; " +
                    "public override IComponent Build(RenderContext c) => new Text(Double.ToString()); }");
        ts.Should().Contain("get double() { return this.n * 2; }");
    }

    [Fact]
    public void GetAccessorWithBody_EmitsGetter()
    {
        var ts = Ts("public class C : StatelessComponent { public string T { get; set; } " +
                    "public string Upper { get { return T.ToUpper(); } } " +
                    "public override IComponent Build(RenderContext c) => new Text(Upper); }");
        ts.Should().Contain("get upper() {");
        ts.Should().Contain("toUpperCase()");
    }

    [Fact]
    public void StaticComputedProperty_EmitsStaticGetter()
    {
        var ts = Ts("public class C : StatelessComponent { public static string AppName => \"eQ\"; " +
                    "public override IComponent Build(RenderContext c) => new Text(AppName); }");
        ts.Should().Contain("static get appName() { return 'eQ'; }");
    }

    [Fact]
    public void AutoPropertyDefault_AppliedInConstructor()
    {
        var ts = Ts("public class C : StatelessComponent { public string Title { get; set; } = \"Hello\"; " +
                    "public override IComponent Build(RenderContext c) => new Text(Title); }");
        // Default applied only if a prop wasn't supplied (base Object.assign runs first).
        ts.Should().Contain("if (this.title === undefined) this.title = 'Hello';");
    }

    [Fact]
    public void ConstructorBody_IsRun()
    {
        var ts = Ts("public class C : StatelessComponent { private readonly int _id; private readonly string _name; " +
                    "public C(int id, string name) { _id = id; _name = name; } " +
                    "public override IComponent Build(RenderContext c) => new Text($\"{_id} {_name}\"); }");
        ts.Should().Contain("this._id = id");
        ts.Should().Contain("this._name = name");
    }

    [Fact]
    public void BareArrayBraceInitializer_BecomesArray()
    {
        var ts = Ts("public class C : StatelessComponent { private static readonly string[] N = { \"a\", \"b\", \"c\" }; " +
                    "public override IComponent Build(RenderContext c) => new Text(N[0]); }");
        ts.Should().Contain("static n: string[] = ['a', 'b', 'c'];");
    }

    [Fact]
    public void TargetTypedNew_InFieldInitializer_ConstructsNamedType()
    {
        var ts = Ts("public record Item(int Id, string Label); " +
                    "public class C : StatelessComponent { private readonly Item _x = new(9, \"z\"); " +
                    "public override IComponent Build(RenderContext c) => new Text(_x.Label); }");
        ts.Should().Contain("new Item(9, 'z')");
        ts.Should().NotContain("_x: Item = {}");
    }

    [Fact]
    public void Subclass_OfUserComponent_WithoutOwnBuild_IsEmitted()
    {
        // NoBuild extends a user component (Mid) and has no Build of its own — it must still be detected
        // and emitted (inheriting Mid's build), not silently dropped.
        var src = "public class Mid : StatelessComponent { public override IComponent Build(RenderContext c) => new Text(\"m\"); } " +
                  "public class NoBuild : Mid { public string Extra() => \"x\"; }";
        var ts = TsOf("NoBuild", src);
        ts.Should().Contain("export class NoBuild extends Mid");
        ts.Should().Contain("extra()");
    }

    [Fact]
    public void AbstractBase_WithConcreteBuild_EmitsBuild_SkipsAbstractMethod()
    {
        var src = "public abstract class CardBase : StatelessComponent { protected abstract string Body(); " +
                  "public override IComponent Build(RenderContext c) => new Text(Body()); } " +
                  "public class InfoCard : CardBase { protected override string Body() => \"info\"; }";
        var baseTs = TsOf("CardBase", src);
        baseTs.Should().Contain("build(context");            // concrete Build is emitted for subclasses to inherit
        baseTs.Should().Contain("this.body()");
        baseTs.Should().NotContain("body() {}");             // abstract method gets no stub
        var subTs = TsOf("InfoCard", src);
        subTs.Should().Contain("export class InfoCard extends CardBase");
        subTs.Should().Contain("body() {");                  // override provides the body
    }

    [Fact]
    public void StaticHelperClass_IsEmittedAsModuleOfStaticMembers()
    {
        var src = "public static class Fmt { public static string Tag(int n) => \"#\" + n; public static string Prefix => \"P\"; } " +
                  "public class C : StatelessComponent { public override IComponent Build(RenderContext c) => new Text(Fmt.Tag(3)); }";
        var fmt = TsOf("Fmt", src);
        fmt.Should().Contain("export class Fmt");
        fmt.Should().Contain("static tag(n: number) {");
        fmt.Should().Contain("static get prefix() { return 'P'; }");
        // The referencing component calls it statically (Fmt.tag), not as an instance method.
        TsOf("C", src).Should().Contain("Fmt.tag(3)");
    }

    [Fact]
    public void PrimitiveTypes_AreNotImported()
    {
        // A property/var of a C# primitive type must never become `import { int } from "./int"`.
        var ts = Ts("public class C : StatelessComponent { public int N { get; set; } public int Double => N * 2; " +
                    "public override IComponent Build(RenderContext c) => new Text(Double.ToString()); }");
        ts.Should().NotContain("from \"./int\"");
        ts.Should().NotContain("from \"./number\"");
    }

    [Fact]
    public void NewTypeParameter_RaisesEQ2003()
    {
        // `new T()` on a generic component can't be transpiled (JS erases generic args), so it must fail
        // the build with a clear diagnostic rather than emit broken `new T()`.
        var src = Header + "public class GenC<T> : StatelessComponent where T : new() { " +
                  "public override IComponent Build(RenderContext c) { var x = new T(); return new Box(); } }";
        var result = new ComponentCompiler().CompileSource(src).Single(r => r.ComponentName == "GenC");
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "EQ2003");
    }

    [Fact]
    public void PrimaryConstructor_ParamsBecomeAssignedFields()
    {
        // C# 12 primary ctor: params are captured as instance state, assigned in the constructor and
        // referenced as `this.<name>` in members. (Previously dropped entirely → `label`/`id` undefined.)
        var ts = Ts("public class C(int id, string label) : StatelessComponent { " +
                    "public override IComponent Build(RenderContext ctx) => new Text(label + id); }");
        ts.Should().Contain("this.id = id");
        ts.Should().Contain("this.label = label");
        ts.Should().Contain("this.label + this.id");
    }

    [Fact]
    public void ServerOnlyMethod_IsNotEmittedAtAll()
    {
        // [ServerOnly] = the method never crosses to the client: no stub, no endpoint, no bytes —
        // which is what lets an SSR prefetch use the whole server surface (HttpClient, EF, the
        // request's services) without tripping the client-boundary validation.
        var ts = Ts("public class C : StatelessComponent { " +
                    "  [ServerOnly] public async Task Prefetch(IServiceProvider services) { await Task.Delay(1); } " +
                    "  public override IComponent Build(RenderContext c) => new Box(); }");
        ts.Should().NotContain("prefetch");
        ts.Should().NotContain("IServiceProvider");
    }

    [Fact]
    public void ConstFromAnotherAssembly_InlinesAsALiteral()
    {
        // A const is a COMPILE-TIME value: inlining is faithful, and it is what lets a page default
        // to a constant declared in an assembly the client bundle never sees (a server-side domain).
        var src = "public static class Limits { public const long Downloads = 627000; public const string Label = \"it's live\"; } " +
                  "public class C : StatelessComponent { private long _n = Limits.Downloads; " +
                  "  public override IComponent Build(RenderContext c) => new Text(Limits.Label); }";
        var ts = TsOf("C", src);
        ts.Should().Contain("627000").And.NotContain("Limits.downloads");
        ts.Should().Contain("'it\\'s live'", "string constants inline escaped");
    }

    [Fact]
    public void EnumMemberAccess_IsNotInlinedAsItsOrdinal()
    {
        // Enum members are constant fields too, but their member-NAME string is the wire contract.
        var ts = Ts("public enum Tone { Soft, Loud } " +
                    "public class C : StatelessComponent { private Tone _t = Tone.Loud; " +
                    "  public override IComponent Build(RenderContext c) => new Box(); }");
        ts.Should().Contain("'loud'").And.NotContain("_t = 1");
    }

    [Fact]
    public void IdentifiersThatAreJsReservedWords_AreRenamed()
    {
        // A module is always strict, so `package`, `interface`, `let`… are illegal identifiers —
        // and they are perfectly ordinary C# names. Declaration AND references must move together.
        var ts = Ts("public class C : StatelessComponent { " +
                    "  public override IComponent Build(RenderContext c) { " +
                    "    var package = \"eQuantic.Core\"; var yield = 2; " +
                    "    return new Text(package + yield); } }");
        ts.Should().Contain("let package_ = 'eQuantic.Core'");
        ts.Should().Contain("package_ + yield_");
        ts.Should().NotContain("let package =").And.NotContain("let yield =");
    }

    [Fact]
    public void AReservedWordAsAParameterOrLambdaArg_IsRenamedToo()
    {
        var ts = Ts("public class C : StatelessComponent { " +
                    "  private string Label(string package) => package.Trim(); " +
                    "  public override IComponent Build(RenderContext c) => " +
                    "    new Text(string.Join(\",\", new[] { \"a\" }.Select(interfaceName => interfaceName))); }");
        ts.Should().Contain("package_: string").And.Contain("package_.trim()");
    }

    [Fact]
    public void OptionalParameters_KeepTheirDefaultsInTheSignature()
    {
        // C# lets a caller omit them; without the default in the signature a call the compiler
        // cannot see (another module, a callback) passes `undefined` into the body — which is how a
        // section helper ended up computing `undefined * 1.05`.
        var ts = Ts("public class C : StatelessComponent { " +
                    "  private string Fmt(string label, int size = 48, bool loud = false) => label + size + loud; " +
                    "  public override IComponent Build(RenderContext c) => new Text(Fmt(\"x\")); }");
        ts.Should().Contain("size: number = 48").And.Contain("loud: boolean = false");
    }

    [Fact]
    public void AnEnumDefault_KeepsItsMemberString()
    {
        var ts = Ts("public enum Tone { Soft, Loud } " +
                    "public class C : StatelessComponent { " +
                    "  private string Say(string text, Tone tone = Tone.Loud) => text + tone; " +
                    "  public override IComponent Build(RenderContext c) => new Text(Say(\"hi\")); }");
        // Enums degrade to their member STRING — the representation the runtime compares.
        ts.Should().Contain("tone: string = 'loud'");
    }

    [Fact]
    public void ATargetTypedNewInsideAStaticDataClassInitializer_IsImported()
    {
        // A data class of nested records — the shape every content catalogue takes. The ELEMENTS are
        // written target-typed (`new(...)`), which states no type name at all: the emitter recovers
        // it from the model (`new Item(...)`), so the IMPORT has to be recovered the same way, or the
        // module loads to "Item is not defined".
        var src = "public record Item(string Label); public record Group(string Title, Item[] Items); " +
                  "public static class Catalogue { public static readonly Group[] Groups = " +
                  "  [new(\"g\", [new(\"a\")])]; } " +
                  "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => new Text(Catalogue.Groups[0].Title); }";
        var ts = TsOfResolved("Catalogue", src);
        ts.Should().Contain("new Item(", "the emitter recovers the target type");
        ts.Should().Contain("from \"./Group\"");
        ts.Should().Contain("from \"./Item\"", "the nested record is constructed here too");
    }

    [Fact]
    public void TypeConstructedOnlyInHelperOrPropertyBody_IsImported()
    {
        // A record constructed ONLY inside a helper-method body (Money) or a property-accessor body must
        // still be imported — not just types named in a property's declared TYPE. Previously the method
        // body was never scanned, so `new Money(..)` emitted "Money is not defined" at module load.
        var src = "public record Money(decimal Amount); public record Tag(string Label); " +
                  "public class C : StatelessComponent { " +
                  "  public Tag DefaultTag => new Tag(\"x\"); " +
                  "  private Money MakeMoney() => new Money(10m); " +
                  "  public override IComponent Build(RenderContext c) => new Box(); }";
        var ts = TsOf("C", src);
        ts.Should().Contain("from \"./Money\"");
        ts.Should().Contain("from \"./Tag\"");
    }

    [Fact]
    public void RecordStaticFactory_StaysStatic()
    {
        // `LegalBlock.P("…")` is the idiomatic way to build a record with a shape argument. Emitting
        // the factory as an INSTANCE method compiles fine and dies at hydration with
        // "X.p is not a function" — SSR renders (C# is happy), the client does not.
        var src = "public record Block(int Kind, string Text) { " +
                  "  public static Block P(string text) => new Block(0, text); " +
                  "  public static Block Empty => new Block(0, \"\"); } " +
                  "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => new Text(Block.P(\"x\").Text); }";
        var ts = TsOf("Block", src);

        ts.Should().Contain("static p(text)");
        ts.Should().Contain("static get empty()");
    }

    [Fact]
    public void RecordParameterDefault_IsCarriedIntoTheConstructor()
    {
        // `string Tag = ""` must construct as '' — falling back to `default(T)` gives null, and the
        // first `entry.Tag.Length` at hydration takes the page down.
        var src = "public record Entry(string Id, string Title, string Tag = \"\", bool Ready = true); " +
                  "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => new Text(new Entry(\"a\", \"b\").Tag); }";
        var ts = TsOf("Entry", src);

        ts.Should().Contain("tag: any = ''");
        ts.Should().Contain("ready: any = true");
    }

    [Fact]
    public void ExtensionOverTheRuntimeVocabulary_StaysAReducedCall()
    {
        // `node.Centered()` lives in Primitives, so the RUNTIME twin carries it as an instance
        // method. Sending it home to a `VisualNodeExtensions` module would import a file the
        // runtime never emits — the page then dies on a missing module at hydration.
        var src = "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => " +
                  "    new Box(new BoxStyle(), new Text(\"hi\").Centered()); }";
        var ts = TsOfResolved("C", src);

        ts.Should().Contain(".centered()");
        ts.Should().NotContain("VisualNodeExtensions");
    }

    [Fact]
    public void ExtensionOverAnAppType_StillGoesHomeToItsModule()
    {
        // The other half of the rule: an extension the APP declares has a module, and the reduced
        // call has to become the static call plus its import.
        var src = "public static class Fmt { public static string Shout(this string s) => s + \"!\"; } " +
                  "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => new Text(\"hi\".Shout()); }";
        var ts = TsOfResolved("C", src);

        ts.Should().Contain("Fmt.shout(");
        ts.Should().Contain("from \"./Fmt\"");
    }

    [Fact]
    public void UserDefinedOperator_BecomesAStaticCall()
    {
        // JavaScript cannot overload `+`. Dropping the operator made `a + b` on two objects
        // concatenate their toString()s — wrong output, no error, nothing to see.
        var src = "public readonly record struct Money(int Amount) { " +
                  "  public static Money operator +(Money a, Money b) => new Money(a.Amount + b.Amount); } " +
                  "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => " +
                  "    new Text((new Money(1) + new Money(2)).Amount.ToString()); }";

        TsOfResolved("Money", src).Should().Contain("static opAdd(a, b)");
        TsOfResolved("C", src).Should().Contain("Money.opAdd(");
    }

    [Fact]
    public void SynthesisedRecordEquality_StaysStructural()
    {
        // A record's `==` is compiler-SYNTHESISED structural equality, not a user's operator —
        // routing it to a static method the emitter never wrote broke every record comparison.
        var src = "public record Point(int X, int Y); " +
                  "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => " +
                  "    new Text((new Point(1, 2) == new Point(1, 2)).ToString()); }";
        var ts = TsOfResolved("C", src);

        ts.Should().NotContain("Point.opEquals");
        ts.Should().Contain("$eq.equals(");
    }

    [Fact]
    public void GenericConstruction_DropsTheTypeArguments()
    {
        // `new Bucket<string>()` is not valid JavaScript — `<` parses as a comparison.
        var src = "public class Bucket<T> { public int Count => 0; } " +
                  "public class C : StatelessComponent { " +
                  "  public override IComponent Build(RenderContext c) => " +
                  "    new Text(new Bucket<string>().Count.ToString()); }";

        TsOfResolved("C", src).Should().Contain("new Bucket()").And.NotContain("Bucket<string>");
    }
}
