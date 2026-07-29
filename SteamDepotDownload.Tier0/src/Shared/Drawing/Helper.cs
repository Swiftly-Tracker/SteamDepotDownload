using System.Globalization;

namespace SteamDepotDownload.Tier0.Shared.Drawing;

public static class Helper
{
    public static (byte? R, byte? G, byte? B, byte? A) ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return (null, null, null, null);
        }

        string s = hex[0] == '#' ? hex[1..] : hex;

        return s.Length switch
        {
            3 => (Nibble(s[0]), Nibble(s[1]), Nibble(s[2]), null),
            4 => (Nibble(s[0]), Nibble(s[1]), Nibble(s[2]), Nibble(s[3])),
            6 => (Byte(s, 0), Byte(s, 2), Byte(s, 4), null),
            8 => (Byte(s, 0), Byte(s, 2), Byte(s, 4), Byte(s, 6)),
            _ => (null, null, null, null),
        };
    }

    private static byte? Nibble(char c)
        => byte.TryParse([c], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? (byte)(v * 16 + v)
            : null;

    private static byte? Byte(string s, int index)
        => byte.TryParse(s.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
}
