using QRCoder;

namespace SteamDepotDownload.Steam.Core.Auth;

internal static class CQrRenderer
{
    internal static string[] Render(string challengeUrl)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(challengeUrl, QRCodeGenerator.ECCLevel.L);

        var qr = new AsciiQRCode(data);
        return qr.GetLineByLineGraphic(1, "██", "  ");
    }
}
