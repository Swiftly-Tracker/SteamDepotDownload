namespace SteamDepotDownload.Steam.Shared.Session;

public sealed record SteamCredentials
{
    public static SteamCredentials Anonymous { get; } = new();

    public static SteamCredentials QrCode { get; } = new() { UseQrCode = true };

    public static SteamCredentials Password(string username, string? password = null, bool remember = false)
        => new() { Username = username, PlainPassword = password, RememberPassword = remember };

    public string? Username { get; init; }

    public string? PlainPassword { get; init; }

    public bool UseQrCode { get; init; }

    public bool RememberPassword { get; init; }

    public bool PreferTwoFactorCode { get; init; }

    public uint? LoginId { get; init; }

    public bool IsAnonymous => !UseQrCode && string.IsNullOrEmpty(Username);
}
