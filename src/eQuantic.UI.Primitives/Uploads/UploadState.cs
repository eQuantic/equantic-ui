namespace eQuantic.UI.Primitives;

/// <summary>Where an <see cref="Upload"/> stands.</summary>
public enum UploadState
{
    /// <summary>Started, and nothing sent yet: the upload is finding out whether the server already
    /// holds part of the file, from an earlier visit.</summary>
    Waiting,

    /// <summary>Sending the file, a part at a time. <see cref="Upload.Sent"/> grows.</summary>
    Sending,

    /// <summary>Stopped by <see cref="Upload.Pause"/>. The server keeps what arrived, and
    /// <see cref="Upload.Resume"/> goes on from there.</summary>
    Paused,

    /// <summary>The whole file arrived. <see cref="Upload.Result"/> is what a Server Action is handed.</summary>
    Completed,

    /// <summary>The upload stopped and will not go on: the policy or the server refused the file, or
    /// the connection did not come back. <see cref="Upload.Error"/> says why.</summary>
    Failed,

    /// <summary>Stopped by <see cref="Upload.Cancel"/>. The server removed what had arrived.</summary>
    Cancelled,
}
