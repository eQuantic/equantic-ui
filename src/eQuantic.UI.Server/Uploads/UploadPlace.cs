using System.Net;

namespace eQuantic.UI.Server;

/// <summary>
/// What weighing a creation answered: the place it holds, or the status and the reason it is refused
/// with, and whether the limit it met frees itself (the owner's unfinished uploads), so a page may wait
/// for it instead of failing.
/// </summary>
internal readonly record struct UploadPlace(UploadReservation? Reservation, HttpStatusCode Status, string? Refusal, bool FreesItself)
{
    public static UploadPlace Refused(HttpStatusCode status, string refusal, bool freesItself = false) =>
        new(null, status, refusal, freesItself);
}
