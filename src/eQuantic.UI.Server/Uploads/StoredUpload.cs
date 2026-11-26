namespace eQuantic.UI.Server;

/// <summary>
/// What the server knows of an upload beyond its bytes, which its <see cref="IUploadStorage"/> keeps
/// from the moment it starts until it is deleted or expires, so its owner and nobody else may continue
/// it or receive it after a restart, and from any instance that shares the storage.
/// </summary>
/// <param name="Id">The upload's id, which the server gives: 32 lowercase hexadecimal digits.</param>
/// <param name="Owner">Whose it is, as the server names an owner: a value to keep as it is.</param>
/// <param name="Policy">The name of the policy it was accepted under.</param>
/// <param name="Name">The file's name, without any folder the device sent.</param>
/// <param name="MimeType">The file's type, as the server reads it from its extension.</param>
/// <param name="Size">The file's size, in bytes, as its creation declared it.</param>
/// <param name="Created">When it started.</param>
/// <param name="Expires">When it is removed, finished or not.</param>
public sealed record StoredUpload(
    string Id, string Owner, string Policy, string Name, string MimeType, long Size, DateTimeOffset Created,
    DateTimeOffset Expires);
