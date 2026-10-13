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
    private ReferencedProject(string assemblyPath, string projectFile)
    {
        AssemblyPath = assemblyPath;
        ProjectFile = projectFile;
        Directory = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(projectFile)!) + Path.DirectorySeparatorChar;
        AssemblyFileName = Path.GetFileName(assemblyPath);
        (AssemblyName, FriendAssemblies) = Describe(assemblyPath);
    }

    /// <summary>The assembly the app's reference list holds for the project.</summary>
    public string AssemblyPath { get; }

    /// <summary>That assembly's file name, which is how a reference list names it.</summary>
    public string AssemblyFileName { get; }

    /// <summary>The project file's full path, which is how the restore graph names the project.</summary>
    public string ProjectFile { get; }

    /// <summary>The project's directory, with a trailing separator, so a path inside it starts with it.</summary>
    public string Directory { get; }

    /// <summary>The assembly's own name, which its compilation takes: another assembly that names it
    /// in a friend declaration or a reference names this.</summary>
    public string AssemblyName { get; }

    /// <summary>The assemblies it declares as friends. A project file's <c>InternalsVisibleTo</c> item
    /// is written into a generated file under <c>obj</c>, which is not among the sources read, so the
    /// declaration is taken from the assembly that carries it.</summary>
    public IReadOnlyList<string> FriendAssemblies { get; }

    /// <summary>Whether <paramref name="file"/> is inside the project's directory.</summary>
    public bool Holds(string file) => Path.GetFullPath(file).StartsWith(Directory, StringComparison.Ordinal);

    /// <summary>
    /// The projects a <c>--projects</c> file lists, each as two lines: the path of the assembly MSBuild
    /// resolved for it, then the path of its project file. The SDK writes it from the references
    /// MSBuild resolved, so nothing in it is set by hand. A path is a line whole: the list was one line
    /// a project, <c>assembly|project file</c>, and a bar, which a path may hold on macOS and Linux,
    /// cut it in the wrong place (#836).
    /// <para>
    /// No file handed is an app that references no project. A file that WAS handed and does not hold
    /// is never that: the list that is not there or holds no project (the SDK hands one only when it
    /// wrote a project), an assembly with no project file after it, an assembly or a project directory
    /// that is not where the list says. Each throws, and the build stops on it (EQ0001). Read as fewer
    /// projects, the build would go on with those projects as assemblies, which is a page written
    /// wrong with no diagnostic (found in review of #827).
    /// </para>
    /// </summary>
    public static IReadOnlyList<ReferencedProject> ReadList(string? projectsFile)
    {
        if (string.IsNullOrEmpty(projectsFile)) return [];
        if (!File.Exists(projectsFile))
            throw new FileNotFoundException($"the list of projects is not there: '{projectsFile}'", projectsFile);

        // A line is a path whole, as a line of the response file is an argument whole: a space that
        // starts or ends a folder's name on macOS and Linux is the folder's. A blank line is no line to
        // pass over: dropped, a list whose project lines were cut paired one project's assembly with
        // the next one's, whose folder exists, and the build read the wrong source (found in review of
        // #843). Only the blank lines that end the file are nothing.
        var all = File.ReadAllLines(projectsFile);
        var count = all.Length;
        while (count > 0 && string.IsNullOrWhiteSpace(all[count - 1])) count--;
        var lines = all.Take(count).Select((text, index) => (Path: text, Number: index + 1)).ToList();
        if (lines.FirstOrDefault(line => string.IsNullOrWhiteSpace(line.Path)) is { Number: > 0 } blank)
            throw new InvalidDataException($"line {blank.Number} is blank, where a path was");
        if (lines.Count % 2 != 0)
            throw new InvalidDataException(
                $"line {lines[^1].Number} is an assembly with no project file after it: '{lines[^1].Path}'");

        var projects = new List<ReferencedProject>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var at = 0; at < lines.Count; at += 2)
        {
            var (assembly, number) = lines[at];
            var (projectFile, projectNumber) = lines[at + 1];
            if (!File.Exists(assembly))
                throw new FileNotFoundException($"the assembly line {number} lists is not there: '{assembly}'", assembly);
            if (Path.GetDirectoryName(Path.GetFullPath(projectFile)) is not { Length: > 0 } directory
                || !System.IO.Directory.Exists(directory))
                throw new DirectoryNotFoundException($"the project line {projectNumber} lists is not there: '{projectFile}'");
            if (!seen.Add(Path.GetFileName(assembly))) continue;
            projects.Add(new ReferencedProject(assembly, Path.GetFullPath(projectFile)));
        }
        if (projects.Count == 0)
            throw new InvalidDataException($"the list of projects holds no project: '{projectsFile}'");
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

    /// <summary>What the assembly says of itself: its name and its friends. Which projects it
    /// references is the restore graph's to say (<see cref="ProjectGraph"/>). An assembly that cannot
    /// be read is named by its file and has no friend.</summary>
    private static (string Name, IReadOnlyList<string> Friends) Describe(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var image = new PEReader(stream);
            var metadata = image.GetMetadataReader();
            return (metadata.GetString(metadata.GetAssemblyDefinition().Name), Friends(metadata));
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException or InvalidOperationException)
        {
            return (Path.GetFileNameWithoutExtension(assemblyPath), []);
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
