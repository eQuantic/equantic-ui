using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// A project the app references with a <c>ProjectReference</c>, directly or through another one: the
/// assembly MSBuild built for it and the directory its source is in. The build reads its source, so a
/// type of it that browser code reaches is a type declared in source, as the app's own are (#624).
/// </summary>
internal sealed class ReferencedProject
{
    private ReferencedProject(string assemblyPath, string directory)
    {
        AssemblyPath = assemblyPath;
        Directory = directory;
        AssemblyFileName = Path.GetFileName(assemblyPath);
        (AssemblyName, AssemblyReferences, FriendAssemblies) = Describe(assemblyPath);
    }

    /// <summary>The assembly the app's reference list holds for the project.</summary>
    public string AssemblyPath { get; }

    /// <summary>That assembly's file name, which is how a reference list names it.</summary>
    public string AssemblyFileName { get; }

    /// <summary>The project's directory, with a trailing separator, so a path inside it starts with it.</summary>
    public string Directory { get; }

    /// <summary>The assembly's own name, which its compilation takes: another assembly that names it
    /// in a friend declaration or a reference names this.</summary>
    public string AssemblyName { get; }

    /// <summary>The names of the assemblies it references, which is what orders the projects.</summary>
    public IReadOnlyList<string> AssemblyReferences { get; }

    /// <summary>The assemblies it declares as friends. A project file's <c>InternalsVisibleTo</c> item
    /// is written into a generated file under <c>obj</c>, which is not among the sources read, so the
    /// declaration is taken from the assembly that carries it.</summary>
    public IReadOnlyList<string> FriendAssemblies { get; }

    /// <summary>Whether <paramref name="file"/> is inside the project's directory.</summary>
    public bool Holds(string file) => Path.GetFullPath(file).StartsWith(Directory, StringComparison.Ordinal);

    /// <summary>
    /// The projects a <c>--projects</c> file lists, one a line: the assembly's path, a <c>|</c>, and
    /// the project file's. The SDK writes it from the references MSBuild resolved, so nothing in it is
    /// set by hand.
    /// <para>
    /// No file handed is an app that references no project. A file that WAS handed and does not hold
    /// is never that: the list that is not there, a line that is not a project, an assembly or a
    /// project directory that is not where the line says. Each throws, and the build stops on it
    /// (EQ0001). Read as fewer projects, the build would go on with those projects as assemblies,
    /// which is a page written wrong with no diagnostic (found in review of #827).
    /// </para>
    /// </summary>
    public static IReadOnlyList<ReferencedProject> ReadList(string? projectsFile)
    {
        if (string.IsNullOrEmpty(projectsFile)) return [];
        if (!File.Exists(projectsFile))
            throw new FileNotFoundException($"the list of projects is not there: '{projectsFile}'", projectsFile);

        var projects = new List<ReferencedProject>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var number = 0;
        foreach (var line in File.ReadAllLines(projectsFile))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var separator = line.IndexOf('|');
            var assembly = separator > 0 ? line[..separator].Trim() : "";
            var projectFile = separator > 0 ? line[(separator + 1)..].Trim() : "";
            if (assembly.Length == 0 || projectFile.Length == 0)
                throw new InvalidDataException($"line {number} is not an assembly, a '|' and a project file: '{line.Trim()}'");
            if (!File.Exists(assembly))
                throw new FileNotFoundException($"the assembly line {number} lists is not there: '{assembly}'", assembly);
            if (Path.GetDirectoryName(Path.GetFullPath(projectFile)) is not { Length: > 0 } directory
                || !System.IO.Directory.Exists(directory))
                throw new DirectoryNotFoundException($"the project line {number} lists is not there: '{projectFile}'");
            if (!seen.Add(Path.GetFileName(assembly))) continue;
            projects.Add(new ReferencedProject(assembly, Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar));
        }
        return projects;
    }

    /// <summary>
    /// The project's own source, by the rule the app's own is read by: every <c>.cs</c> under its
    /// directory outside its <c>bin</c> and <c>obj</c>, and its generated global usings. A
    /// sub-directory that holds a project file of its own is another project's, and is left to it.
    /// </summary>
    public IReadOnlyList<string> SourceFiles()
    {
        var files = new List<string>();
        void Walk(string directory, bool isRoot)
        {
            files.AddRange(System.IO.Directory.GetFiles(directory, "*.cs").Order(StringComparer.Ordinal));
            foreach (var child in System.IO.Directory.GetDirectories(directory).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(child);
                if (isRoot && name is "bin" or "obj") continue;
                if (System.IO.Directory.EnumerateFiles(child, "*.*proj").Any()) continue;
                Walk(child, isRoot: false);
            }
        }
        Walk(Directory, isRoot: true);
        files.AddRange(ProjectCompilationHelper.GetGeneratedGlobalUsingsFiles(Directory).Order(StringComparer.Ordinal));
        return files;
    }

    /// <summary>What the assembly says of itself: its name, the assemblies it references and its
    /// friends. An assembly that cannot be read is named by its file and references nothing.</summary>
    private static (string Name, IReadOnlyList<string> References, IReadOnlyList<string> Friends) Describe(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var image = new PEReader(stream);
            var metadata = image.GetMetadataReader();
            var name = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            var references = metadata.AssemblyReferences
                .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                .ToList();
            return (name, references, Friends(metadata));
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException or InvalidOperationException)
        {
            return (Path.GetFileNameWithoutExtension(assemblyPath), [], []);
        }
    }

    /// <summary>The assembly's <c>InternalsVisibleTo</c> declarations, each one's single string argument.</summary>
    private static IReadOnlyList<string> Friends(MetadataReader metadata)
    {
        var friends = new List<string>();
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (metadata.GetString(type.Name) != "InternalsVisibleToAttribute"
                || metadata.GetString(type.Namespace) != "System.Runtime.CompilerServices")
                continue;
            // A custom attribute's value: the two-byte prolog, then the constructor's arguments, of
            // which this one has a single string.
            var value = metadata.GetBlobReader(attribute.Value);
            if (value.Length < 3 || value.ReadUInt16() != 1) continue;
            if (value.ReadSerializedString() is { Length: > 0 } friend) friends.Add(friend);
        }
        return friends;
    }
}
