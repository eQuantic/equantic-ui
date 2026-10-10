using Microsoft.AspNetCore.Authorization;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>The policy an app writes for a room's topic: the requirement its handler decides.</summary>
internal sealed class RoomMemberRequirement : IAuthorizationRequirement;
