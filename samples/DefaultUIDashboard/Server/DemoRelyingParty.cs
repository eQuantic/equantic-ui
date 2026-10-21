using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The sample's relying party, as little of one as shows a passkey's round trip (#1017): it writes
/// the Web Authentication options, remembers each challenge it issued until a ceremony spends it,
/// and reads from the device's answer the ceremony and the challenge it carries.
/// <para>
/// It verifies NO signature and no attestation. A relying party does that with a Web Authentication
/// library (fido2-net-lib, in .NET), which writes the same JSON this hands out and verifies the JSON
/// it takes back; signing anyone in on the checks below would let in any page that learned a
/// challenge. <c>[ServerOnly]</c>: no page builds it, the passkey page's server actions call it.
/// </para>
/// </summary>
[ServerOnly]
public static class DemoRelyingParty
{
    /// <summary>Each challenge issued and not spent yet, with the ceremony it was issued for.</summary>
    private static readonly ConcurrentDictionary<string, string> Challenges = new(StringComparer.Ordinal);

    /// <summary>The passkeys registered here, by credential id.</summary>
    private static readonly ConcurrentDictionary<string, byte> Passkeys = new(StringComparer.Ordinal);

    /// <summary>The one account this sample knows, as the handle its authenticator keeps.</summary>
    private static readonly string UserHandle = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>The options to create a passkey, excluding the ones already registered here, so a
    /// device that holds one answers that it does.</summary>
    public static string CreationOptions() => JsonSerializer.Serialize(new
    {
        challenge = Issue("webauthn.create"),
        rp = new { name = "eQuantic Console" },
        user = new { id = UserHandle, name = "ana@equantic.tech", displayName = "Ana Beatriz" },
        pubKeyCredParams = new[] { new { type = "public-key", alg = -7 }, new { type = "public-key", alg = -257 } },
        excludeCredentials = Passkeys.Keys.Select(id => new { type = "public-key", id }).ToArray(),
        authenticatorSelection = new { residentKey = "required", userVerification = "preferred" },
        timeout = 60_000,
    });

    /// <summary>The options to sign in. A passkey is discoverable, so the device offers its own even
    /// before this server knows one.</summary>
    public static string RequestOptions() => JsonSerializer.Serialize(new
    {
        challenge = Issue("webauthn.get"),
        allowCredentials = Passkeys.Keys.Select(id => new { type = "public-key", id }).ToArray(),
        userVerification = "preferred",
        timeout = 60_000,
    });

    /// <summary>Takes a new passkey's credential and says what it proved.</summary>
    public static string Register(string credential)
    {
        var (id, problem) = Read(credential, "webauthn.create");
        if (problem is not null) return $"Refused: {problem}.";
        Passkeys[id] = 0;
        return $"Registered passkey {Short(id)}: its client data carried the challenge this server issued. "
            + "A relying party would now verify its attestation with a Web Authentication library.";
    }

    /// <summary>Takes an assertion and says what it proved.</summary>
    public static string SignIn(string credential)
    {
        var (id, problem) = Read(credential, "webauthn.get");
        if (problem is not null) return $"Refused: {problem}.";
        if (!Passkeys.ContainsKey(id)) return $"Refused: passkey {Short(id)} was never registered here.";
        return $"Signed in with passkey {Short(id)}: its client data carried the challenge this server issued. "
            + "A relying party would now verify its signature with a Web Authentication library.";
    }

    private static string Issue(string ceremony)
    {
        var challenge = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        Challenges[challenge] = ceremony;
        return challenge;
    }

    /// <summary>The credential's id, and what is wrong with it, read from its client data: the
    /// ceremony it says it was, and whether this server issued its challenge for that ceremony. The
    /// challenge is spent either way, so an answer is never taken twice.</summary>
    private static (string Id, string? Problem) Read(string credential, string ceremony)
    {
        using var answer = JsonDocument.Parse(credential);
        var id = answer.RootElement.GetProperty("id").GetString() ?? "";
        var encoded = answer.RootElement.GetProperty("response").GetProperty("clientDataJSON").GetString() ?? "";
        using var clientData = JsonDocument.Parse(Base64Url.DecodeFromChars(encoded));
        var type = clientData.RootElement.GetProperty("type").GetString();
        var challenge = clientData.RootElement.GetProperty("challenge").GetString() ?? "";
        if (!Challenges.TryRemove(challenge, out var issuedFor) || issuedFor != ceremony)
            return (id, "the challenge is not one this server issued for it");
        return type == ceremony ? (id, null) : (id, $"the client data says {type}, not {ceremony}");
    }

    private static string Short(string id) => id.Length <= 8 ? id : id[..8];
}
