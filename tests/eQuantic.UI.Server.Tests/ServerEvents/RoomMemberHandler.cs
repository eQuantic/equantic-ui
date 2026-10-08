using Microsoft.AspNetCore.Authorization;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>
/// An ordinary ASP.NET Core authorization handler with the topic as its resource: a request is a
/// member of the room its <c>X-Room</c> header names, read from the topic's own HttpContext, against
/// the template's <c>roomId</c>.
/// </summary>
internal sealed class RoomMemberHandler : AuthorizationHandler<RoomMemberRequirement, ServerTopicContext>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RoomMemberRequirement requirement, ServerTopicContext topic)
    {
        if (topic.HttpContext.Request.Headers["X-Room"] == topic.Values["roomId"]) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
