// The readme inside every package is the repository's README, with each picture at the address of
// the package's own release (#880).
//
// README.md names a picture by its path in the repository, which GitHub draws on every branch and
// nuget.org does not draw at all. So a package's readme is written when it packs
// (WritePackageReadme, in Directory.Build.targets), with each picture at its address under the
// release's tag. A test runs that target. This reads what a pack put in the package, for every
// package and from every system that packs one: a project the target did not run for, or a host
// that changed the text on its way, shows here and nowhere else.
//
// usage: dotnet run scripts/check-package-readme.cs -- <folder of packages> [<README.md>]
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

if (args.Length is < 1 or > 2 || !Directory.Exists(args[0]))
{
    Console.WriteLine("::error::usage: check-package-readme.cs <folder of packages> [<README.md>]");
    return 2;
}

const string Picture = "](assets/readme/";
var readme = File.ReadAllText(args.Length == 2 ? args[1] : "README.md");
var pictures = readme.Split(Picture).Length - 1;
var packages = Directory.GetFiles(args[0], "*.nupkg");
var failures = 0;
foreach (var package in packages)
{
    var name = Path.GetFileName(package);
    using var archive = ZipFile.OpenRead(package);
    var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
    string version;
    using (var metadata = nuspec.Open())
        version = XDocument.Load(metadata).Descendants().First(element => element.Name.LocalName == "version").Value;

    var packed = archive.GetEntry("README.md");
    if (packed is null)
    {
        Console.WriteLine($"::error::{name} holds no README.md");
        failures++;
        continue;
    }

    using var bytes = new MemoryStream();
    using (var content = packed.Open()) content.CopyTo(bytes);
    var expected = readme.Replace(Picture, $"](https://raw.githubusercontent.com/eQuantic/equantic-ui/v{version}/assets/readme/");
    if (bytes.ToArray().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected))) continue;

    var written = Encoding.UTF8.GetString(bytes.ToArray());
    var at = written.AsSpan().CommonPrefixLength(expected);
    string Near(string text) => text[Math.Max(0, at - 40)..Math.Min(text.Length, at + 40)].ReplaceLineEndings("\\n");
    Console.WriteLine($"::error::{name}: its readme is not README.md with each picture at the address of v{version}. At character {at} it reads \"{Near(written)}\" where \"{Near(expected)}\" belongs");
    failures++;
}

Console.WriteLine($"packages read: {packages.Length}, each with README.md and its {pictures} pictures at the address of its own release unless an error above says otherwise");
if (packages.Length == 0)
{
    Console.WriteLine("::error::the folder holds no package");
    failures++;
}
return failures == 0 ? 0 : 1;
