namespace eQuantic.UI.Native.Engine.Vulkan;

/// <summary>
/// What an answer to <c>vkAllocateDescriptorSets</c> means. A pool has a fixed number of sets, and
/// "this pool has no room" is a fact about the POOL, not a failure of the device: the next set comes
/// from another pool. Read as an error, it ended the process at the 1,025th live set (#858).
/// <para>
/// Two codes say it. OUT_OF_POOL_MEMORY is the one Vulkan 1.1 defines for it; FRAGMENTED_POOL is what
/// an implementation that predates it, or one that counts differently, answers for the same pool,
/// and it is what the Android emulator's driver answered when this was measured.
/// </para>
/// </summary>
internal static class DescriptorPoolStatus
{
    /// <summary>VK_ERROR_OUT_OF_POOL_MEMORY.</summary>
    internal const int OutOfPoolMemory = -1000069000;

    /// <summary>VK_ERROR_FRAGMENTED_POOL.</summary>
    internal const int FragmentedPool = -12;

    /// <summary>True when the pool asked has no room for the set, and another pool would.</summary>
    internal static bool IsFull(int result) => result is OutOfPoolMemory or FragmentedPool;
}
