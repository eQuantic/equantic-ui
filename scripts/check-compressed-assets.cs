// Every module a publish carries has a .gz and a .br that decompress to it, byte for byte.
//
// The Web SDK compresses the static web assets a build DEFINES, so the variants are only as fresh
// as the moment the assets were defined. When that moment came before eqc wrote its output, a
// build compressed the previous build's modules under the new names (#361): a browser negotiating
// gzip or brotli got yesterday's code, and a present file, a status code or a file size all look
// the same either way. Only the bytes tell, and .NET reads both formats itself.
//
// usage: dotnet run scripts/check-compressed-assets.cs -- <folder of published modules>
using System.IO.Compression;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.WriteLine("::error::usage: check-compressed-assets.cs <folder of published modules>");
    return 2;
}

var modules = Directory.GetFiles(args[0], "*.js");
var failures = 0;
foreach (var module in modules)
{
    var expected = File.ReadAllBytes(module);
    foreach (var (suffix, decompress) in new (string, Func<Stream, Stream>)[]
    {
        (".gz", encoded => new GZipStream(encoded, CompressionMode.Decompress)),
        (".br", encoded => new BrotliStream(encoded, CompressionMode.Decompress)),
    })
    {
        var variant = module + suffix;
        if (!File.Exists(variant))
        {
            Console.WriteLine($"::error::{variant} is not in the publish");
            failures++;
            continue;
        }
        using var decoded = new MemoryStream();
        using (var reader = decompress(File.OpenRead(variant))) reader.CopyTo(decoded);
        if (!decoded.ToArray().AsSpan().SequenceEqual(expected))
        {
            Console.WriteLine($"::error::{variant} does not decompress to the module published beside it");
            failures++;
        }
    }
}

Console.WriteLine($"modules published: {modules.Length}, each with a .gz and a .br of itself unless an error above says otherwise");
if (modules.Length == 0)
{
    Console.WriteLine("::error::the publish holds no module");
    failures++;
}
return failures == 0 ? 0 : 1;
