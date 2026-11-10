using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(eQuantic.UI.Server.HotReload.ServerEditsHandler))]

namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// Where .NET tells the server an edit was applied to it: the runtime calls every handler an assembly
/// declares once the edit's changes are in place, so markup rendered after this call renders the edit.
/// </summary>
internal static class ServerEditsHandler
{
    internal static void UpdateApplication(Type[]? updatedTypes) => ServerEdits.OfThisProcess.Take();
}
