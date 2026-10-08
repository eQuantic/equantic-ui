using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type reached through its namespace or an alias is imported only where the browser has a twin of it
/// (#625): a .NET type has none, so naming it imports nothing, never a module of the app's that shares its
/// simple name. `new Values()` over `using Values = List&lt;int&gt;` imported the app's own `List`, and
/// `System.Math.PI` the app's `Math`, which hid the browser's (found by Copilot's second review of #705).
/// </summary>
public class TypeImportScopeTests
{
    [Fact]
    public void ADotNetTypeNamedThroughAnAliasOrItsNamespace_ImportsNoModuleOfItsName()
    {
        var room = CompileScanned("""
            using Values = System.Collections.Generic.List<int>;
            using M = System.Math;

            namespace App
            {
                public class List { public int N = 1; }
                public class Math { public int N = 2; }

                public static class Room
                {
                    public static int Counted() { var v = new Values { 1, 2 }; return v.Count; }
                    public static double Pi() => System.Math.PI + M.E;
                }
            }
            """, "Room");

        room.Errors.Should().BeEmpty();
        room.TypeScript.Should().NotContain("from \"./List\"").And.NotContain("from \"./Math\"");
    }

    /// <summary>A file compiled as eqc compiles a project's: scanned by the dependency resolver first, whose
    /// module set decides every import, then compiled with it.</summary>
    private static CompilationResult CompileScanned(string source, string component)
    {
        var dir = Directory.CreateTempSubdirectory("eq-imports-").FullName;
        try
        {
            var path = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(path, source);
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            return compiler.CompileFile(path).Single(result => result.ComponentName == component);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
