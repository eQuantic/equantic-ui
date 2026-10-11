namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// The size and the orientation a swapchain's images are made in. Both come from the WINDOW the
/// renderer draws, not from what the surface last reported, because the renderer draws upright and
/// at the window's own size, and the compositor does the rest (#794).
/// </summary>
internal static class SwapchainShape
{
    /// <summary>VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR.</summary>
    internal const uint Identity = 0x1;

    /// <summary>
    /// The transform the swapchain says its images already carry: IDENTITY wherever the surface
    /// takes it, so the compositor turns the image to the panel. Declaring the surface's current
    /// transform instead tells the compositor the image is ALREADY rotated, which the renderer never
    /// does, so a phone held sideways would show the app sideways. The price is a present that
    /// answers SUBOPTIMAL while the two differ, which <see cref="SwapchainStatus.Usable"/> reads as
    /// shown.
    /// </summary>
    internal static uint PreTransform(uint supported, uint current) =>
        (supported & Identity) != 0 ? Identity : current;

    /// <summary>
    /// The window's size, inside the range the surface accepts. Where the surface fixes its extent
    /// (a desktop window: the smallest and the largest are both the current one) the range IS that
    /// extent; where it scales whatever it is given (Android), the window's size passes through.
    /// Null when the range has no area, as a minimised desktop window's does: a zero extent is not a
    /// swapchain Vulkan can make.
    /// <para>
    /// The surface's current extent is not asked. On Android it still describes the OLD buffer when
    /// the surface reports the rotated window, so a swapchain rebuilt from it stayed one rotation
    /// behind: the landscape window drew a portrait buffer, squeezed into its top half.
    /// </para>
    /// </summary>
    internal static (uint Width, uint Height)? Extent(int width, int height,
        (uint Width, uint Height) smallest, (uint Width, uint Height) largest)
    {
        if (largest.Width == 0 || largest.Height == 0) return null;
        return (Math.Clamp((uint)Math.Max(width, 1), Math.Max(smallest.Width, 1), largest.Width),
            Math.Clamp((uint)Math.Max(height, 1), Math.Max(smallest.Height, 1), largest.Height));
    }
}
