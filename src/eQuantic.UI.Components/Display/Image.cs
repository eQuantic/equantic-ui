using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Images;

namespace eQuantic.UI.Components.Display;

/// <summary>
/// Specifies how the image should be loaded.
/// </summary>
public enum ImageLoading
{
    /// <summary>
    /// Default. Defer loading of the image until it reaches a calculated distance from the viewport.
    /// </summary>
    Lazy,
    /// <summary>
    /// Load the image immediately. Use for Above-the-Fold images.
    /// </summary>
    Eager
}

/// <summary>
/// Specifies the placeholder to use while the image is loading.
/// </summary>
public enum ImagePlaceholder
{
    /// <summary>
    /// No placeholder.
    /// </summary>
    None,
    /// <summary>
    /// Uses BlurDataURL as a blurred background.
    /// </summary>
    Blur,
    /// <summary>
    /// Uses a solid color or custom CSS Shimmer.
    /// </summary>
    Empty
}

/// <summary>
/// A high-performance image component inspired by Next.js.
/// Optimizes LCP, prevents CLS, and handles lazy loading.
/// </summary>
public class Image : StatelessComponent
{
    /// <summary>
    /// The source URL of the image.
    /// </summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>
    /// Accessible description of the image.
    /// </summary>
    public string Alt { get; set; } = string.Empty;

    /// <summary>
    /// Width of the image in pixels (or null for fill/responsive).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the image in pixels (or null for fill/responsive).
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Defines if the image should fill the parent container.
    /// If true, Width and Height are ignored and the image is absolute positioned.
    /// </summary>
    public bool Fill { get; set; }

    /// <summary>
    /// Loading strategy. Default is Lazy.
    /// </summary>
    public ImageLoading Loading { get; set; } = ImageLoading.Lazy;

    /// <summary>
    /// If true, the image will be considered high priority and preload.
    /// Sets loading="eager" and fetchpriority="high".
    /// </summary>
    public bool Priority { get; set; }

    /// <summary>
    /// Responsive variants for the image.
    /// </summary>
    public string? SrcSet { get; set; }

    /// <summary>
    /// Image sizes for responsive layout.
    /// </summary>
    public string? Sizes { get; set; }

    /// <summary>
    /// Placeholder type to show while loading.
    /// </summary>
    public ImagePlaceholder Placeholder { get; set; } = ImagePlaceholder.None;

    /// <summary>
    /// A small Data URL to use as a blurred placeholder (usually 10x10px).
    /// Required if Placeholder is Blur.
    /// </summary>
    public string? BlurDataURL { get; set; }

    /// <summary>
    /// Object-fit CSS property for the image. Default: cover (if Fill is true).
    /// </summary>
    public string? ObjectFit { get; set; }

    /// <summary>
    /// Object-position CSS property for the image.
    /// </summary>
    public string? ObjectPosition { get; set; }

    /// <summary>
    /// Enable server-side image optimization (resize, format conversion, quality).
    /// When null, falls back to global setting from UseImageOptimization().
    /// Only applies to local images (paths starting with /).
    /// </summary>
    public bool? Optimize { get; set; }

    /// <summary>
    /// Image quality (1-100). Only used when optimization is active.
    /// Default: 0 (uses global default from ImageOptimizationOptions).
    /// </summary>
    public int Quality { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var attributes = new Dictionary<string, string>
        {
            ["src"] = Src,
            ["alt"] = Alt,
            ["loading"] = Priority ? "eager" : Loading.ToString().ToLowerInvariant(),
            ["decoding"] = "async"
        };

        if (Priority)
        {
            attributes["fetchpriority"] = "high";
        }

        // Server-side image optimization: generate optimized src/srcset URLs
        var shouldOptimize = Optimize ?? ImageOptimizationState.IsEnabled;
        if (shouldOptimize && !string.IsNullOrEmpty(Src) && Src.StartsWith("/"))
        {
            var q = Quality > 0 ? Quality : ImageOptimizationState.DefaultQuality;
            var escapedUrl = Uri.EscapeDataString(Src);

            if (Width.HasValue && !Fill)
            {
                // Fixed-size image: generate 1x and 2x density descriptors
                var w1x = ImageOptimizationState.SnapToAllowed(Width.Value);
                var w2x = ImageOptimizationState.SnapToAllowed(Width.Value * 2);
                attributes["src"] = $"/_equantic/image?url={escapedUrl}&w={w1x}&q={q}";
                attributes["srcset"] =
                    $"/_equantic/image?url={escapedUrl}&w={w1x}&q={q} 1x, " +
                    $"/_equantic/image?url={escapedUrl}&w={w2x}&q={q} 2x";
            }
            else
            {
                // Responsive image: generate width descriptors for all configured sizes
                var allWidths = ImageOptimizationState.AllowedWidths;
                var srcsetParts = allWidths.Select(w =>
                    $"/_equantic/image?url={escapedUrl}&w={w}&q={q} {w}w");
                attributes["srcset"] = string.Join(", ", srcsetParts);
                attributes["src"] = $"/_equantic/image?url={escapedUrl}&w={allWidths[allWidths.Length - 1]}&q={q}";

                if (string.IsNullOrEmpty(Sizes))
                    attributes["sizes"] = "100vw";
                else
                    attributes["sizes"] = Sizes;
            }
        }
        else
        {
            // No optimization: use manual srcset/sizes if provided
            if (!string.IsNullOrEmpty(SrcSet)) attributes["srcset"] = SrcSet;
            if (!string.IsNullOrEmpty(Sizes)) attributes["sizes"] = Sizes;
        }

        var styleList = new List<string>();
        var classList = new List<string> { "equantic-image" };
        if (!string.IsNullOrEmpty(ClassName)) classList.Add(ClassName);

        if (Fill)
        {
            classList.Add("absolute inset-0 w-full h-full");
            styleList.Add($"object-fit: {ObjectFit ?? "cover"}");
            if (!string.IsNullOrEmpty(ObjectPosition)) styleList.Add($"object-position: {ObjectPosition}");
        }
        else
        {
            if (Width.HasValue) attributes["width"] = Width.Value.ToString();
            if (Height.HasValue) attributes["height"] = Height.Value.ToString();
            
            styleList.Add("color: transparent"); // Hide Alt text during load if possible
        }

        // Handle Blur Placeholder
        if (Placeholder == ImagePlaceholder.Blur && !string.IsNullOrEmpty(BlurDataURL))
        {
            styleList.Add($"background-image: url(\"{BlurDataURL}\")");
            styleList.Add("background-size: cover");
            styleList.Add("background-position: center");
            styleList.Add("filter: blur(20px)");
            styleList.Add("transition: filter 0.3s ease-out");
            
            // Script to remove blur once loaded
            attributes["onload"] = "this.style.filter='none'";
        }

        if (styleList.Count > 0) attributes["style"] = string.Join("; ", styleList);
        attributes["class"] = string.Join(" ", classList);

        var img = new DynamicElement
        {
            TagName = "img",
            CustomAttributes = attributes
        };

        if (Fill)
        {
            // Wrapper for fill mode to maintain relative context if not provided by parent
            return new DynamicElement
            {
                TagName = "div",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = "relative w-full h-full overflow-hidden"
                },
                Children = { img }
            };
        }

        return img;
    }
}
