// CLI entry point for eQuantic.UI Compiler
using System.Diagnostics;
using System.Text;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: eqc <source-dir> <output-dir> [--bun <path>] [--watch]");
    return 1;
}

// TrimEntries: the SDK passes the source dirs as "$(MSBuildProjectDirectory);$(_StandardComponentsDir)",
// and _StandardComponentsDir is defined as multi-line XML, so its value carries leading whitespace/
// newline. Without trimming, Directory.Exists fails on the standard-components path → the built-in
// components (Grid/Box/…) are never transpiled, and pages referencing them break with "X is not defined".
var sourceDirs = args[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var outputDir = args[1].Trim();
var bunPath = args.ToList().Contains("--bun") ? args[args.ToList().IndexOf("--bun") + 1] : null;
var isWatchMode = args.Any(a => a == "--watch");
// --refs <file>: a newline-delimited list of the assemblies MSBuild resolved for this project
// (@(ReferencePathWithRefAssemblies)). Feeding these to the semantic model makes eqc's type resolution
// identical to the real csc build — eQuantic.UI.Core/Components, NuGet and project references all resolve,
// independent of TargetFramework or configuration. Without it, base types like HtmlElement.Children
// (IList<IComponent>) stay unresolved and member calls degrade to naive camel-casing (.Add → .add).
var refsFile = args.ToList().Contains("--refs") ? args[args.ToList().IndexOf("--refs") + 1] : null;
// --ref-sources <file>: newline-delimited DIRECTORIES whose .cs join the compilation as
// SEMANTIC-ONLY reference sources (never transpiled). This is what lets eqc inline external
// constants at the use site — an icon pack's `static readonly IconGlyph` needs its INITIALIZER,
// which metadata (via --refs) does not carry; the pack's tools/source supplies it here.
var refSourcesFile = args.ToList().Contains("--ref-sources") ? args[args.ToList().IndexOf("--ref-sources") + 1] : null;
// --generated <dir>: the SOURCE-GENERATOR output directory for the configuration being built. The
// SDK knows which one that is and eqc cannot: a project built in both configurations has a
// generated tree under obj/Debug AND obj/Release, and sweeping obj would take every generated type
// twice — which resolves to neither, while the C# build stays perfectly happy.
var generatedDir = args.ToList().Contains("--generated") ? args[args.ToList().IndexOf("--generated") + 1].Trim() : null;

// Determine intermediate directory based on primary source dir
var primarySourceDir = sourceDirs[0];
var intermediateDir = Path.Combine(primarySourceDir, "obj", "eQuantic", "ts");

var compiler = new ComponentCompiler();

// Create full project compilation for better type resolution
// This enables the compiler to resolve types defined in external files
Compilation? projectCompilation = null;
try
{
    // Write to a log file for debugging since MSBuild might not show console output
    var logPath = Path.Combine(intermediateDir, "compilation.log");
    Directory.CreateDirectory(intermediateDir);
    File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Starting compilation setup\n");

    Console.WriteLine("🔍 Attempting to load project compilation...");

    // Collect source files only from the PRIMARY source directory (user's project)
    // Skip standard components directory as those are already compiled
    var allSourceFiles = new List<string>();

    if (Directory.Exists(primarySourceDir))
    {
        Console.WriteLine($"   Scanning project directory: {primarySourceDir}");
        var files = ProjectCompilationHelper.GetProjectSourceFiles(primarySourceDir).ToList();
        Console.WriteLine($"   Found {files.Count} source files in project");
        allSourceFiles.AddRange(files);

        // Feed the SDK-generated global usings (obj/*.GlobalUsings.g.cs) to the compilation so the
        // semantic model resolves BCL types used unqualified — e.g. Dictionary<RecordKey, V> under
        // <ImplicitUsings> — exactly as the real build does, without hardcoding a namespace list.
        var globalUsings = ProjectCompilationHelper.GetGeneratedGlobalUsingsFiles(primarySourceDir).ToList();
        if (globalUsings.Count > 0)
        {
            Console.WriteLine($"   Including {globalUsings.Count} generated global-usings file(s)");
            allSourceFiles.AddRange(globalUsings);
        }

        // SOURCE-GENERATOR output: part of the program csc compiled, so a page calling into it
        // must resolve here too — see GetCompilerGeneratedFiles.
        var generated = ProjectCompilationHelper.GetCompilerGeneratedFiles(primarySourceDir, generatedDir).ToList();
        if (generated.Count > 0)
        {
            Console.WriteLine($"   Including {generated.Count} compiler-generated file(s)");
            allSourceFiles.AddRange(generated);
        }
    }

    // Reference-only sources (icon packs etc.): their .cs enter the compilation so the semantic model
    // can reach constant initializers for inlining, but they are NOT in sourceDirs so nothing here is
    // transpiled to a module.
    if (!string.IsNullOrEmpty(refSourcesFile) && File.Exists(refSourcesFile))
    {
        var refSourceCount = 0;
        foreach (var dir in File.ReadAllLines(refSourcesFile).Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in ProjectCompilationHelper.GetProjectSourceFiles(dir))
            {
                allSourceFiles.Add(file);
                refSourceCount++;
            }
        }
        if (refSourceCount > 0)
            Console.WriteLine($"   Including {refSourceCount} reference-source file(s) for constant inlining");
    }

    if (allSourceFiles.Count > 0)
    {
        var assemblyPaths = new List<string>();

        // Preferred path: the exact reference set MSBuild resolved for this project, handed to us via
        // --refs. This is what csc itself compiles against, so the semantic model resolves every type
        // the real build does (eQuantic.UI.*, NuGet, project references) with no TFM/config guessing.
        var usingExplicitRefs = false;
        if (!string.IsNullOrEmpty(refsFile) && File.Exists(refsFile))
        {
            foreach (var line in File.ReadAllLines(refsFile))
            {
                var path = line.Trim();
                if (path.Length > 0 && File.Exists(path))
                    assemblyPaths.Add(path);
            }
            usingExplicitRefs = assemblyPaths.Count > 0;
            Console.WriteLine($"   Using {assemblyPaths.Count} MSBuild-resolved references from {Path.GetFileName(refsFile)}");

            // An EMPTY --refs file is not a reason to fall back — it is a reason to stop. The bin-tree
            // fallback below exists for a human running eqc by hand with no MSBuild at all; reaching it
            // from a real build means the semantic model is gone, and the compiler's degraded path
            // passes named arguments through in SYNTACTIC order. That renders a component with the
            // wrong values and reports nothing, which is the single worst outcome this compiler has.
            // Fail the build and say what to do about it.
            if (!usingExplicitRefs)
            {
                Console.Error.WriteLine(
                    $"{refsFile}(1,1): error EQ0002: the MSBuild reference list is empty, so the " +
                    "semantic model would be built from an incomplete compilation and named arguments " +
                    "could be emitted in the wrong order. Rebuild the project (dotnet build) to " +
                    "regenerate it; if this persists, the CompileEQuanticUI target ran without " +
                    "FindReferenceAssembliesForReferences.");
                return 1;
            }
        }

        if (!usingExplicitRefs)
        {
            // Fallback for direct CLI use without --refs: the running BCL plus any eQuantic/System/Roslyn
            // assemblies emitted to the project's bin tree. Scan every TFM/config dir (deduping by file
            // name) instead of hardcoding one — the old net8.0 hardcode silently dropped all references.
            assemblyPaths.Add(typeof(object).Assembly.Location);   // System.Private.CoreLib
            assemblyPaths.Add(typeof(Enumerable).Assembly.Location); // System.Linq
            assemblyPaths.Add(typeof(List<>).Assembly.Location);   // System.Collections

            var binRoot = Path.Combine(primarySourceDir, "bin");
            if (Directory.Exists(binRoot))
            {
                var seen = new HashSet<string>(assemblyPaths.Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
                foreach (var dll in Directory.GetFiles(binRoot, "*.dll", SearchOption.AllDirectories))
                {
                    var fileName = Path.GetFileName(dll);
                    var isCandidate =
                        fileName.StartsWith("eQuantic.UI", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase);
                    if (isCandidate && seen.Add(fileName))
                        assemblyPaths.Add(dll);
                }
                Console.WriteLine($"   Found {assemblyPaths.Count - 3} assemblies in bin tree (fallback)");
            }
        }

        Console.WriteLine($"   Creating compilation for {allSourceFiles.Count} files with {assemblyPaths.Count} references...");

        // Get project name from .csproj
        var csprojFiles = Directory.GetFiles(primarySourceDir, "*.csproj", SearchOption.TopDirectoryOnly);
        var assemblyName = csprojFiles.Length > 0 ? Path.GetFileNameWithoutExtension(csprojFiles[0]) : "DynamicAssembly";

        // When we have the complete MSBuild reference set, use it verbatim. Injecting the runtime BCL
        // impl assemblies on top of the targeting-pack ref assemblies would duplicate identities and
        // muddy resolution, so only add the standard fallback set when --refs was not supplied.
        projectCompilation = ProjectCompilationHelper.CreateCompilationFromSources(
            allSourceFiles,
            assemblyPaths,
            assemblyName: assemblyName,
            addStandardReferences: !usingExplicitRefs
        );

        compiler.SetProjectCompilation(projectCompilation);
        Console.WriteLine($"📚 Loaded project compilation: {assemblyName} ({allSourceFiles.Count} files, {assemblyPaths.Count} refs)");

        // Log success
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] SUCCESS: Loaded {assemblyName} with {allSourceFiles.Count} files\n");
    }
    else
    {
        Console.WriteLine("⚠️  No source files found for project compilation");
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] WARNING: No source files found\n");
    }
}
catch (Exception ex)
{
    // If project compilation fails, continue with minimal compilation
    Console.WriteLine($"⚠️  Project compilation failed: {ex.Message}");
    Console.WriteLine($"   Stack: {ex.StackTrace}");
    Console.WriteLine("   Using minimal compilation per file");

    var logPath = Path.Combine(intermediateDir, "compilation.log");
    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n{ex.StackTrace}\n");
}

// Initialize dependency resolver by scanning component directories
var dependencyResolver = new ComponentDependencyResolver();
var componentDirectories = new List<string>(sourceDirs);

// Also scan standard component library locations relative to build tool
var buildDir = AppContext.BaseDirectory;
var standardComponentsPath = Path.GetFullPath(Path.Combine(buildDir, "..", "..", "..", "..", "eQuantic.UI.Web.Components"));
if (Directory.Exists(standardComponentsPath))
{
    componentDirectories.Add(standardComponentsPath);
}

dependencyResolver.GeneratedDirectory = generatedDir;
dependencyResolver.ScanSourceDirectories(componentDirectories);
compiler.SetDependencyResolver(dependencyResolver);

var hasBun = !string.IsNullOrEmpty(bunPath) && File.Exists(bunPath);

if (isWatchMode)
{
    Console.WriteLine($"👀 eQuantic.UI: Watching {sourceDirs.Length} directories...");
}
else
{
    Console.WriteLine($"🔨 eQuantic.UI: Compiling components from {sourceDirs.Length} directories");
}

Console.WriteLine($"   Intermediate: {intermediateDir}");
Console.WriteLine($"   Output:       {outputDir}");

// Initial compilation
var initialBuildHadErrors = CompileAndBundle();

if (isWatchMode)
{
    var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100));
    // Watch all source directories
    var watchers = new List<FileSystemWatcher>();
    foreach (var dir in sourceDirs)
    {
        var watcher = new FileSystemWatcher(dir, "*.cs");
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;

        FileSystemEventHandler onChanged = (sender, e) =>
        {
            if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                e.FullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                return;
                
            debouncer.Debounce(() => {
                Console.WriteLine($"🔄 Change detected in {Path.GetFileName(e.FullPath)}. Recompiling...");
                CompileAndBundle();
            });
        };

        watcher.Changed += onChanged;
        watcher.Created += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += (s, e) => onChanged(s, e);
        watcher.EnableRaisingEvents = true;
        watchers.Add(watcher);
    }
    
    await Task.Delay(-1);
}

// Non-zero exit fails the MSBuild `Exec` step when transpilation hit unsupported constructs.
return initialBuildHadErrors ? 1 : 0;

// Format a diagnostic in MSBuild's canonical form so `Exec` recognises it and fails/warns the build:
//   path(line,col): error EQ2001: message
static string FormatDiagnostic(eQuantic.UI.Compiler.CompilationError d, string severity)
{
    var code = string.IsNullOrEmpty(d.Code) ? "EQ0000" : d.Code;
    var path = string.IsNullOrEmpty(d.SourcePath) ? "eQuantic.UI" : d.SourcePath;
    var line = d.Line > 0 ? d.Line : 1;
    var col = d.Column > 0 ? d.Column : 1;
    return $"{path}({line},{col}): {severity} {code}: {d.Message}";
}

// Runs the source-map merge with one JS runtime. Returns true only on a clean exit (0); returns false
// on a launch failure OR a non-zero exit (e.g. the bun JS VM crashing on an AVX-less VM) — never throws,
// so the caller can fall back to another runtime and the build is never broken by map merging.
static bool TryRunMerge(string fileName, string script, string mapFile)
{
    try
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = $"\"{script}\" \"{mapFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        p.Start();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

// Returns true if compilation produced errors (so the process can exit non-zero and fail the build).
bool CompileAndBundle()
{
    try
    {
        var hasErrors = false;
        // Which names are already taken — see EmittedTwins for why a name collision is an error.
        var written = new eQuantic.UI.Compiler.Services.EmittedTwins();
        var entryPoints = new List<string>();
        
        if (!Directory.Exists(intermediateDir)) Directory.CreateDirectory(intermediateDir);
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        // GENERATED sources are compiled alongside the app's own: a generator that writes something
        // a page CALLS (a factory surface built from the app's components) has to become a module,
        // or the page names a binding the bundle never defines. They are paired with the project
        // dir so their entry-point rule is the project's own; nothing here is an entry point unless
        // it is a page. Files a generator wrote for other purposes (ASP.NET's public Program, say)
        // pass through the parser and come back empty, exactly as a non-component always has.
        var compileUnits = sourceDirs
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Select(file => (Dir: dir, File: file)))
            .Concat(ProjectCompilationHelper.GetCompilerGeneratedFiles(primarySourceDir, generatedDir)
                .Select(file => (Dir: primarySourceDir, File: file)))
            .GroupBy(unit => unit.Dir);

        foreach (var group in compileUnits)
        {
            var dir = group.Key;
            var files = group.Select(unit => unit.File);

            foreach (var file in files)
            {
                var isGenerated = file.Contains($"{Path.DirectorySeparatorChar}generated{Path.DirectorySeparatorChar}");
                if (!isGenerated &&
                    (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                     file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
                    continue;

                // Try to compile - parser will return empty if not a component
                // This removes restrictions on naming, inheritance patterns, aliases, etc.
                var results = compiler.CompileFile(file);
                
                foreach (var result in results)
                {
                    // Warnings surface whether or not compilation succeeded (e.g. an unconverted
                    // construct emitted verbatim). Errors are reported below and fail the build.
                    foreach (var warning in result.Warnings)
                    {
                        Console.Error.WriteLine(FormatDiagnostic(warning, "warning"));
                    }

                    if (result.Success)
                    {
                        var tsPath = Path.Combine(intermediateDir, $"{result.ComponentName}.ts");

                        // A twin is named for its TYPE, not its namespace, so two types with one
                        // name write the same file and the second wins. Nothing said so: C# is
                        // happy — the namespaces differ — and the page died in the browser with
                        // the OTHER type's fields, which is the worst way to learn.
                        // Namespace-qualified, because that is what makes two claims ONE type.
                        // The FILE is still keyed by the bare name — see EmittedTwins for why both
                        // halves are needed, and why either alone gets a real case wrong.
                        var identity = string.IsNullOrEmpty(result.Namespace)
                            ? result.ComponentName
                            : $"{result.Namespace}.{result.ComponentName}";
                        var claim = written.Claim(result.ComponentName, identity, file, result.TypeScript,
                            path => Path.GetRelativePath(dir, path), out var message);
                        if (claim == eQuantic.UI.Compiler.Services.TwinClaim.Collision)
                        {
                            Console.Error.WriteLine($"{file}(1,1): error EQ1005: {message}");
                            hasErrors = true;
                            continue;
                        }
                        // One type across declarations eqc cannot merge. The build goes on with
                        // the module already written; the members left out are said out loud,
                        // because their absence shows up only in the browser.
                        if (claim == eQuantic.UI.Compiler.Services.TwinClaim.Divided)
                        {
                            Console.Error.WriteLine($"{file}(1,1): warning EQ1006: {message}");
                            continue;
                        }
                        // The same module already on disk: writing it again would rewrite its map
                        // with one pointing at a different C# file.
                        if (claim == eQuantic.UI.Compiler.Services.TwinClaim.Repeat) continue;

                        File.WriteAllText(tsPath, result.TypeScript);
                        
                        if (!string.IsNullOrEmpty(result.SourceMap))
                        {
                            var mapPath = tsPath + ".map";
                            File.WriteAllText(mapPath, result.SourceMap);
                            File.AppendAllText(tsPath, $"\n//# sourceMappingURL={result.ComponentName}.ts.map");
                        }
                        
                        var relativePath = Path.GetRelativePath(dir, file);
                        // A PAGE is always an entry point, wherever its file sits: the server loads
                        // it by name, so a page that is only transpiled is a 404 the build never
                        // mentioned. The positional rule stays as well — a project laid out the
                        // conventional way keeps every module it had before.
                        var isEntryPoint = result.IsPage
                            || (!isGenerated && dir == sourceDirs[0]
                                && (relativePath.StartsWith("Pages")
                                    || !relativePath.Contains(Path.DirectorySeparatorChar)));
                        if (isEntryPoint)
                        {
                            entryPoints.Add(tsPath);
                        }
                    }
                    else
                    {
                        hasErrors = true;
                        foreach (var error in result.Errors)
                        {
                            Console.Error.WriteLine(FormatDiagnostic(error, "error"));
                        }
                    }
                }
            }
        }

        if (hasErrors) return true;

        // Track L D3/D12: the per-culture string catalogs, from exactly the keys the compiled
        // tree used. The fallback chain is FLATTENED here — a key present only in the neutral
        // resx appears in every culture's catalog — so the runtime does a flat lookup and never
        // reimplements .NET's resolution.
        EmitStringCatalogs(compiler, outputDir, primarySourceDir);

        if (!hasBun && entryPoints.Count > 0)
        {
            // The embedded Bun is a hard requirement — bundling is what produces the page JS. A
            // missing binary must fail the build loudly, never "succeed" without output.
            Console.Error.WriteLine("❌ Embedded Bun not found — the eQuantic.UI build requires the " +
                "bundled Bun (restore the platform Runtime package, e.g. eQuantic.UI.Runtime.Osx64).");
            return true;
        }

        if (hasBun && entryPoints.Count > 0)
        {
            // --root pins the output-path base to the intermediate TS dir (where every entry .ts lives),
            // so bun writes entries FLAT as "<Page>.js" in outDir. Without it bun infers the root from the
            // common ancestor of the absolute entry paths (the repo/cwd) and nests entries under that
            // relative path (e.g. wwwroot/_equantic/samples/.../ts/Dashboard.js), which the boot — loading
            // the flat "/_equantic/<Page>.js" — then 404s on.
            var bunArgs = $"build {string.Join(" ", entryPoints.Select(p => $"\"{p}\""))} --outdir \"{outputDir}\" --root \"{intermediateDir}\" --splitting --sourcemap --minify-syntax --minify-whitespace --target browser --external @equantic/runtime";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bunPath!,
                    Arguments = bunArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("❌ Bun compilation failed:");
                Console.Error.WriteLine(error);
                return true;
            }

            // Post-process source maps to merge C# -> TS and TS -> JS
            var jsMapFiles = Directory.GetFiles(outputDir, "*.js.map", SearchOption.AllDirectories);
            var scriptsDir = Path.Combine(AppContext.BaseDirectory, "Scripts");
            var mergeMapsScript = Path.Combine(scriptsDir, "merge-maps.js");

            if (File.Exists(mergeMapsScript))
            {
                // Ensure dependency is installed
                var nodeModulesDir = Path.Combine(scriptsDir, "node_modules", "@ampproject", "remapping");
                if (!Directory.Exists(nodeModulesDir))
                {
                    Console.WriteLine("📦 Installing remapping dependency in SDK CLI...");
                    var installProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = bunPath!,
                            Arguments = "add @ampproject/remapping",
                            WorkingDirectory = scriptsDir,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    installProcess.Start();
                    installProcess.WaitForExit();
                }
                foreach (var mapFile in jsMapFiles)
                {
                    // Source-map merge is BEST-EFFORT (C#-level debugging only) — it must never fail
                    // the build. Prefer the embedded bun (zero Node by default); if bun can't start OR
                    // exits non-zero (e.g. the bun JS VM crashes on some VMs), fall back to a system
                    // `node`. If neither works, warn and move on.
                    if (!TryRunMerge(bunPath!, mergeMapsScript, mapFile) &&
                        !TryRunMerge("node", mergeMapsScript, mapFile))
                    {
                        Console.Error.WriteLine($"⚠️ Map merging skipped for {Path.GetFileName(mapFile)} (no working JS runtime).");
                    }
                }
            }
        }

        Console.WriteLine($"✅ Built at {DateTime.Now:HH:mm:ss}");
        return false;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"eQuantic.UI(1,1): error EQ0001: Compilation crash: {ex.Message}");
        return true;
    }
}


static void EmitStringCatalogs(eQuantic.UI.Compiler.ComponentCompiler compiler, string outputDir,
    string appSourceDir)
{
    // Two contributions, and the difference is not a preference — it is what each side can KNOW.
    // The app's own resx contributes the keys its pages actually read (D3: unused keys never
    // ship), because this compilation sees those reads. A LIBRARY's resx (the SDK's own
    // SdkResources above all) is read by components already transpiled into runtime.js at the
    // SDK's build, so no read is visible here at all — it contributes WHOLE, which is exactly
    // D14's promise that an app with zero resx still announces "Marcado" to a pt-BR reader.
    var appRoot = Path.GetFullPath(appSourceDir);
    var libraryResources = compiler.DiscoveredResources
        .Where(pair => pair.Value.Length > 0
            && !Path.GetFullPath(pair.Value).StartsWith(appRoot, StringComparison.Ordinal))
        .ToList();

    var uses = compiler.ResourceUses;
    if (uses.Count == 0 && libraryResources.Count == 0) return;

    var neutral = new SortedDictionary<string, string>(StringComparer.Ordinal);
    var cultures = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    SortedDictionary<string, string> CatalogFor(string culture) =>
        culture.Length == 0
            ? neutral
            : cultures.TryGetValue(culture, out var existing)
                ? existing
                : cultures[culture] = new SortedDictionary<string, string>(StringComparer.Ordinal);

    foreach (var (id, designerPath) in libraryResources)
    {
        foreach (var (culture, path) in eQuantic.UI.Compiler.Services.ResxFiles.VariantsFor(designerPath))
        {
            var values = eQuantic.UI.Compiler.Services.ResxFiles.Read(path);
            if (values is null) continue;
            var target = CatalogFor(culture);
            foreach (var (key, value) in values) target[$"{id}/{key}"] = value;
        }
    }

    foreach (var use in uses)
    {
        var variants = eQuantic.UI.Compiler.Services.ResxFiles.VariantsFor(use.DesignerPath).ToList();
        if (variants.Count == 0)
        {
            Console.Error.WriteLine($"⚠️  strings: no .resx found beside {use.DesignerPath} — " +
                $"'{use.Id}' resolves on the server and native, but the web catalog has no values.");
            continue;
        }
        foreach (var (culture, path) in variants)
        {
            var values = eQuantic.UI.Compiler.Services.ResxFiles.Read(path);
            if (values is null) continue;
            var target = CatalogFor(culture);
            foreach (var key in use.Keys)
            {
                if (values.TryGetValue(key, out var value)) target[$"{use.Id}/{key}"] = value;
                else if (culture.Length == 0)
                    Console.Error.WriteLine($"⚠️  strings: key '{key}' is read by a page but " +
                        $"missing from {path}.");
            }
        }
    }

    // D12: flatten — named cultures inherit every neutral-only key, so the client lookup is flat.
    foreach (var strings in cultures.Values)
        foreach (var (key, value) in neutral)
            strings.TryAdd(key, value);

    // D7: the FORMATTING facts ride with the strings. `{0:C}` needs an ISO currency code (Intl
    // takes no symbol and no browser API maps a locale to one), and .NET's `d`/`D`/`g` are
    // shorthand for the culture's OWN patterns, which Intl's presets do not reproduce (its short
    // date drops en-US to a two-digit year). Both come from .NET here, where .NET is running, and
    // travel inside the catalog the shell already inlines and setCulture already fetches.
    // Written AFTER the flatten so a neutral key can never overwrite them.
    AddFormatFacts(neutral, System.Globalization.CultureInfo.InvariantCulture);
    foreach (var (culture, strings) in cultures)
    {
        try
        {
            AddFormatFacts(strings, System.Globalization.CultureInfo.GetCultureInfo(culture));
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            // A resx named for a culture this machine does not know: the strings still ship; only
            // the formatting facts are missing, and the client falls back to Intl's own presets.
            Console.Error.WriteLine($"⚠️  strings: '{culture}' is not a culture this machine knows — "
                + "its catalog ships without formatting facts.");
        }
    }

    var stringsDir = Path.Combine(outputDir, "strings");
    Directory.CreateDirectory(stringsDir);
    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };
    File.WriteAllText(Path.Combine(stringsDir, "neutral.json"),
        System.Text.Json.JsonSerializer.Serialize(neutral, options));
    foreach (var (culture, strings) in cultures)
        File.WriteAllText(Path.Combine(stringsDir, culture + ".json"),
            System.Text.Json.JsonSerializer.Serialize(strings, options));
    Console.WriteLine($"🌐 Strings: {neutral.Count} keys → neutral" +
        (cultures.Count > 0 ? " + " + string.Join(", ", cultures.Keys) : ""));
}

/// <summary>
/// The reserved `$`-prefixed keys a catalog carries besides its strings (Track L D7): the ISO
/// currency code and the culture's own date/time patterns. `$` cannot collide with a resource id,
/// which is always a C# identifier followed by `/`.
/// </summary>
static void AddFormatFacts(SortedDictionary<string, string> catalog,
    System.Globalization.CultureInfo culture)
{
    if (!culture.Equals(System.Globalization.CultureInfo.InvariantCulture))
    {
        try
        {
            catalog["$currency"] = new System.Globalization.RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            // A NEUTRAL culture ("pt", "es") names no country, so it has no currency of its own —
            // .NET's own RegionInfo refuses it. The client then prints the generic ¤, exactly as
            // .NET does for a culture that cannot name a currency.
        }
    }

    var format = culture.DateTimeFormat;
    catalog["$dateShort"] = format.ShortDatePattern;
    catalog["$dateLong"] = format.LongDatePattern;
    catalog["$timeShort"] = format.ShortTimePattern;
    catalog["$timeLong"] = format.LongTimePattern;
    catalog["$monthDay"] = format.MonthDayPattern;
    catalog["$yearMonth"] = format.YearMonthPattern;
}

class Debouncer
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;

    public Debouncer(TimeSpan delay)
    {
        _delay = delay;
    }

    public void Debounce(Action action)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Delay(_delay, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                action();
            }
        });
    }
}
