namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// What the presentation engine's answers mean to a frame. Acquire and present each answer with a
/// VkResult, and two of those are facts about the WINDOW rather than failures: the swapchain no
/// longer matches the surface (OUT_OF_DATE, an error code: nothing was acquired or shown, so it is
/// rebuilt first), or no longer matches it exactly (SUBOPTIMAL, a SUCCESS code: the image was
/// acquired or shown all the same).
/// <para>
/// SUBOPTIMAL is positive. Both call sites used to test for -1000001002, a value Vulkan does not
/// define, under a comment that named it SUBOPTIMAL, so the real one fell through to
/// <see cref="Vk.Check"/> and was thrown: Android answers it on the first present after a rotation,
/// and every rotation of every Android app crashed it (#794).
/// </para>
/// </summary>
internal static class SwapchainStatus
{
    /// <summary>VK_SUBOPTIMAL_KHR.</summary>
    internal const int Suboptimal = 1000001003;

    /// <summary>VK_ERROR_OUT_OF_DATE_KHR.</summary>
    internal const int OutOfDate = -1000001004;

    /// <summary>VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR.</summary>
    internal const uint Identity = 0x1;

    /// <summary>
    /// True when the call did its work (an image acquired, a frame shown), false when the swapchain
    /// has to be rebuilt before it can; any other answer is an error, and throws.
    /// </summary>
    internal static bool Usable(int result, string operation) => result switch
    {
        Vk.Success or Suboptimal => true,
        OutOfDate => false,
        _ => throw new InvalidOperationException($"Vulkan {operation} failed: VkResult {result}."),
    };

    /// <summary>
    /// The transform the swapchain says its images already carry. IDENTITY wherever the surface
    /// takes it: the renderer draws upright, in the window's own orientation and at the window's own
    /// extent, and the compositor turns the image to the panel. Declaring the surface's current
    /// transform instead tells the compositor the image is ALREADY rotated, which the renderer never
    /// does, so a phone held sideways would show the app sideways. The price is a present that
    /// answers SUBOPTIMAL while the two differ, which <see cref="Usable"/> reads as shown.
    /// </summary>
    internal static uint PreTransform(uint supported, uint current) =>
        (supported & Identity) != 0 ? Identity : current;
}
