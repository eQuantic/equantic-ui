using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A PLAIN class's field declared with no initializer still has a value in C# — its type's default —
/// and its twin has to start with the same one.
/// <para>
/// The constructor of a plain class wrote only the fields that HAD an initializer and skipped the
/// rest, so a <c>bool</c> began life <c>undefined</c>: neither true nor false, read as true by
/// <c>!flag</c> and as false by <c>flag === false</c>, and refused by tsc outright (TS2564) as soon as
/// nothing in the constructor assigned it. Found when the code editor's engine — a plain class with a
/// <c>private bool _dragging;</c> — became transpiled code. The component path had answered this from
/// the type for a while (<see cref="FieldDefaultTests"/>); a plain class is the same C#.
/// </para>
/// </summary>
public class PlainClassFieldDefaultTests
{
    private const string Source = """
        public enum Mode { Idle, Busy }

        public sealed class Machine
        {
            private bool _dragging;
            private int _count;
            private float _scale;
            private long _ticks;
            private decimal _total;
            private char _last;
            private Mode _mode;
            private string? _name;
            private int _seeded = 7;

            public bool Dragging => _dragging;
        }
        """;

    private static string Emit()
    {
        var results = new ComponentCompiler().CompileSource(Source, "Machine.cs").ToList();
        var machine = results.Single(r => r.ComponentName == "Machine");
        machine.Success.Should().BeTrue(string.Join("; ", machine.Errors.Select(e => e.Message)));
        return machine.TypeScript;
    }

    [Fact]
    public void AFieldWithNoInitializerStartsAtItsTypesDefault()
    {
        var ts = Emit();

        ts.Should().Contain("this._dragging = false;");
        ts.Should().Contain("this._count = 0;");
        ts.Should().Contain("this._scale = 0;");
        ts.Should().Contain("this._ticks = $eq.num.long(0);");
        ts.Should().Contain("this._total = $eq.num.dec(0);");
        ts.Should().Contain("this._last = '\\0';");
        // An enum is its member NAME at runtime: the default is the zero member.
        ts.Should().Contain("this._mode = 'idle';");
    }

    [Fact]
    public void AnInitializerStillWins_AndAReferenceStaysUnset()
    {
        var ts = Emit();

        ts.Should().Contain("this._seeded = 7;");
        // A reference type's default is null, which an unassigned member already reads as — writing
        // it would be noise, and the field is declared nullable so tsc has nothing to ask.
        ts.Should().NotContain("this._name =");
    }

    [Fact]
    public void TheHelperItNamesIsImported()
    {
        var ts = Emit();

        // The long and decimal defaults are `$eq` calls, and a module that names `$eq` without
        // importing it throws at load.
        ts.Should().MatchRegex(@"import \{[^}]*\$eq[^}]*\} from");
    }
}
