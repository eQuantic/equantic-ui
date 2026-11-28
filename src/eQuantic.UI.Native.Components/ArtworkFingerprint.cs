using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a <see cref="VectorDrawing"/> draws, as a number (#1937): its viewBox and every shape's path,
/// fill, stroke, width, rule and opacity, hashed once per instance (a <see cref="ConditionalWeakTable{TKey, TValue}"/>,
/// so an instance the app lets go takes its entry with it). Two drawings that draw the same thing share
/// a tile, whether the app keeps one instance or builds a new one each frame, which a key by reference
/// would composite again every frame. It also says whether any shape asks for <c>currentColor</c>, the
/// only way the tint reaches the pixels, so artwork in its own colours is one tile in every mode.
/// </summary>
internal sealed class ArtworkFingerprint
{
    private static readonly ConditionalWeakTable<VectorDrawing, ArtworkFingerprint> Prints = new();

    private ArtworkFingerprint(UInt128 hash, bool inherits)
    {
        Hash = hash;
        Inherits = inherits;
    }

    /// <summary>The first 128 bits of the SHA-256 of the drawing's content.</summary>
    public UInt128 Hash { get; }

    /// <summary>Whether a shape is painted in the colour the drawing is tinted with.</summary>
    public bool Inherits { get; }

    /// <summary>The fingerprint of <paramref name="artwork"/>, computed the first time it is asked for.</summary>
    public static ArtworkFingerprint Of(VectorDrawing artwork) => Prints.GetValue(artwork, Compute);

    private static ArtworkFingerprint Compute(VectorDrawing artwork)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var number = new byte[4];
        void Float(float value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(number, value);
            hash.AppendData(number);
        }
        void Text(string value)
        {
            Float(value.Length);
            hash.AppendData(Encoding.UTF8.GetBytes(value));
        }
        void Colour(Color color)
        {
            number[0] = color.R; number[1] = color.G; number[2] = color.B; number[3] = color.A;
            hash.AppendData(number);
        }
        var inherits = false;
        void Paint(VectorPaint paint)
        {
            Float((float)paint.Kind);
            Colour(paint.Color);
            inherits |= paint.Kind == VectorPaintKind.Inherit;
            if (paint.Gradient is not { } run) return;
            Float(run.X1); Float(run.Y1); Float(run.X2); Float(run.Y2); Float(run.Radius);
            Float(run.UserSpace ? 1 : 0);
            Float(run.Stops.Count);
            foreach (var stop in run.Stops)
            {
                Float(stop.Offset);
                Colour(stop.Color);
            }
        }

        Float(artwork.MinX); Float(artwork.MinY); Float(artwork.Width); Float(artwork.Height);
        Float(artwork.Shapes.Count);
        foreach (var shape in artwork.Shapes)
        {
            Text(shape.Path);
            Paint(shape.Fill);
            Paint(shape.Stroke);
            Float(shape.StrokeWidth);
            Float(shape.EvenOdd ? 1 : 0);
            Float(shape.Opacity);
        }
        var digest = hash.GetHashAndReset();
        return new ArtworkFingerprint(BinaryPrimitives.ReadUInt128LittleEndian(digest), inherits);
    }
}
