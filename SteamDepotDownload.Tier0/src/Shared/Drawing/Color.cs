using System.Runtime.InteropServices;

namespace SteamDepotDownload.Tier0.Shared.Drawing;

[StructLayout(LayoutKind.Sequential, Size = 4)]
public struct Color : IEquatable<Color>
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public Color(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public Color(byte r, byte g, byte b) : this(r, g, b, byte.MaxValue)
    {
    }

    public Color(int r, int g, int b) : this((byte)r, (byte)g, (byte)b, byte.MaxValue)
    {
    }
    public Color(int r, int g, int b, int a) : this((byte)r, (byte)g, (byte)b, (byte)a)
    {
    }
    public Color(char r, char g, char b, char a) : this((byte)r, (byte)g, (byte)b, (byte)a)
    {
    }

    public Color(char r, char g, char b) : this((byte)r, (byte)g, (byte)b, byte.MaxValue)
    {
    }

    public static Color FromInt32(int color)
    {
        return new Color((byte)(color >> 24), (byte)(color >> 16), (byte)(color >> 8), (byte)color);
    }

    public static Color FromBuiltin(System.Drawing.Color color)
    {
        return new Color(color.R, color.G, color.B, color.A);
    }

    public static Color FromHex(string hex)
    {
        var (r, g, b, a) = Helper.ParseHexColor(hex);
        return r == null || g == null || b == null
            ? throw new ArgumentException($"Invalid hex color format: '{hex}'. Expected #RGB, #RGBA, #RRGGBB, or #RRGGBBAA.")
            : new Color(r.Value, g.Value, b.Value, a ?? 255);
    }

    public readonly System.Drawing.Color ToBuiltin()
    {
        return System.Drawing.Color.FromArgb(A, R, G, B);
    }

    public readonly int ToInt32()
    {
        return (R << 24) | (G << 16) | (B << 8) | A;
    }

    public readonly string ToHex(bool includeAlpha = false)
    {
        return $"#{R:X2}{G:X2}{B:X2}{(includeAlpha ? $"{A:X2}" : "")}";
    }

    public readonly bool Equals(Color other)
    {
        return R == other.R && G == other.G && B == other.B && A == other.A;
    }

    public override string ToString() => $"Color({R}, {G}, {B}, {A})";
    public override bool Equals(object? obj) => obj is Color color && Equals(color);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    public static Color AliceBlue = FromBuiltin(System.Drawing.Color.AliceBlue);
    public static Color AntiqueWhite = FromBuiltin(System.Drawing.Color.AntiqueWhite);
    public static Color Aqua = FromBuiltin(System.Drawing.Color.Aqua);
    public static Color Aquamarine = FromBuiltin(System.Drawing.Color.Aquamarine);
    public static Color Azure = FromBuiltin(System.Drawing.Color.Azure);
    public static Color Beige = FromBuiltin(System.Drawing.Color.Beige);
    public static Color Bisque = FromBuiltin(System.Drawing.Color.Bisque);
    public static Color Black = FromBuiltin(System.Drawing.Color.Black);
    public static Color BlanchedAlmond = FromBuiltin(System.Drawing.Color.BlanchedAlmond);
    public static Color Blue = FromBuiltin(System.Drawing.Color.Blue);
    public static Color BlueViolet = FromBuiltin(System.Drawing.Color.BlueViolet);
    public static Color Brown = FromBuiltin(System.Drawing.Color.Brown);
    public static Color BurlyWood = FromBuiltin(System.Drawing.Color.BurlyWood);
    public static Color CadetBlue = FromBuiltin(System.Drawing.Color.CadetBlue);
    public static Color Chartreuse = FromBuiltin(System.Drawing.Color.Chartreuse);
    public static Color Chocolate = FromBuiltin(System.Drawing.Color.Chocolate);
    public static Color Coral = FromBuiltin(System.Drawing.Color.Coral);
    public static Color CornflowerBlue = FromBuiltin(System.Drawing.Color.CornflowerBlue);
    public static Color Cornsilk = FromBuiltin(System.Drawing.Color.Cornsilk);
    public static Color Crimson = FromBuiltin(System.Drawing.Color.Crimson);
    public static Color Cyan = FromBuiltin(System.Drawing.Color.Cyan);
    public static Color DarkBlue = FromBuiltin(System.Drawing.Color.DarkBlue);
    public static Color DarkCyan = FromBuiltin(System.Drawing.Color.DarkCyan);
    public static Color DarkGoldenrod = FromBuiltin(System.Drawing.Color.DarkGoldenrod);
    public static Color DarkGray = FromBuiltin(System.Drawing.Color.DarkGray);
    public static Color DarkGreen = FromBuiltin(System.Drawing.Color.DarkGreen);
    public static Color DarkKhaki = FromBuiltin(System.Drawing.Color.DarkKhaki);
    public static Color DarkMagenta = FromBuiltin(System.Drawing.Color.DarkMagenta);
    public static Color DarkOliveGreen = FromBuiltin(System.Drawing.Color.DarkOliveGreen);
    public static Color DarkOrange = FromBuiltin(System.Drawing.Color.DarkOrange);
    public static Color DarkOrchid = FromBuiltin(System.Drawing.Color.DarkOrchid);
    public static Color DarkRed = FromBuiltin(System.Drawing.Color.DarkRed);
    public static Color DarkSalmon = FromBuiltin(System.Drawing.Color.DarkSalmon);
    public static Color DarkSeaGreen = FromBuiltin(System.Drawing.Color.DarkSeaGreen);
    public static Color DarkSlateBlue = FromBuiltin(System.Drawing.Color.DarkSlateBlue);
    public static Color DarkSlateGray = FromBuiltin(System.Drawing.Color.DarkSlateGray);
    public static Color DarkTurquoise = FromBuiltin(System.Drawing.Color.DarkTurquoise);
    public static Color DarkViolet = FromBuiltin(System.Drawing.Color.DarkViolet);
    public static Color DeepPink = FromBuiltin(System.Drawing.Color.DeepPink);
    public static Color DeepSkyBlue = FromBuiltin(System.Drawing.Color.DeepSkyBlue);
    public static Color DimGray = FromBuiltin(System.Drawing.Color.DimGray);
    public static Color DodgerBlue = FromBuiltin(System.Drawing.Color.DodgerBlue);
    public static Color Firebrick = FromBuiltin(System.Drawing.Color.Firebrick);
    public static Color FloralWhite = FromBuiltin(System.Drawing.Color.FloralWhite);
    public static Color ForestGreen = FromBuiltin(System.Drawing.Color.ForestGreen);
    public static Color Fuchsia = FromBuiltin(System.Drawing.Color.Fuchsia);
    public static Color Gainsboro = FromBuiltin(System.Drawing.Color.Gainsboro);
    public static Color GhostWhite = FromBuiltin(System.Drawing.Color.GhostWhite);
    public static Color Gold = FromBuiltin(System.Drawing.Color.Gold);
    public static Color Goldenrod = FromBuiltin(System.Drawing.Color.Goldenrod);
    public static Color Gray = FromBuiltin(System.Drawing.Color.Gray);
    public static Color Green = FromBuiltin(System.Drawing.Color.Green);
    public static Color GreenYellow = FromBuiltin(System.Drawing.Color.GreenYellow);
    public static Color Honeydew = FromBuiltin(System.Drawing.Color.Honeydew);
    public static Color HotPink = FromBuiltin(System.Drawing.Color.HotPink);
    public static Color IndianRed = FromBuiltin(System.Drawing.Color.IndianRed);
    public static Color Indigo = FromBuiltin(System.Drawing.Color.Indigo);
    public static Color Ivory = FromBuiltin(System.Drawing.Color.Ivory);
    public static Color Khaki = FromBuiltin(System.Drawing.Color.Khaki);
    public static Color Lavender = FromBuiltin(System.Drawing.Color.Lavender);
    public static Color LavenderBlush = FromBuiltin(System.Drawing.Color.LavenderBlush);
    public static Color LawnGreen = FromBuiltin(System.Drawing.Color.LawnGreen);
    public static Color LemonChiffon = FromBuiltin(System.Drawing.Color.LemonChiffon);
    public static Color LightBlue = FromBuiltin(System.Drawing.Color.LightBlue);
    public static Color LightCoral = FromBuiltin(System.Drawing.Color.LightCoral);
    public static Color LightCyan = FromBuiltin(System.Drawing.Color.LightCyan);
    public static Color LightGoldenrodYellow = FromBuiltin(System.Drawing.Color.LightGoldenrodYellow);
    public static Color LightGray = FromBuiltin(System.Drawing.Color.LightGray);
    public static Color LightGreen = FromBuiltin(System.Drawing.Color.LightGreen);
    public static Color LightPink = FromBuiltin(System.Drawing.Color.LightPink);
    public static Color LightSalmon = FromBuiltin(System.Drawing.Color.LightSalmon);
    public static Color LightSeaGreen = FromBuiltin(System.Drawing.Color.LightSeaGreen);
    public static Color LightSkyBlue = FromBuiltin(System.Drawing.Color.LightSkyBlue);
    public static Color LightSlateGray = FromBuiltin(System.Drawing.Color.LightSlateGray);
    public static Color LightSteelBlue = FromBuiltin(System.Drawing.Color.LightSteelBlue);
    public static Color LightYellow = FromBuiltin(System.Drawing.Color.LightYellow);
    public static Color Lime = FromBuiltin(System.Drawing.Color.Lime);
    public static Color LimeGreen = FromBuiltin(System.Drawing.Color.LimeGreen);
    public static Color Linen = FromBuiltin(System.Drawing.Color.Linen);
    public static Color Magenta = FromBuiltin(System.Drawing.Color.Magenta);
    public static Color Maroon = FromBuiltin(System.Drawing.Color.Maroon);
    public static Color MediumAquamarine = FromBuiltin(System.Drawing.Color.MediumAquamarine);
    public static Color MediumBlue = FromBuiltin(System.Drawing.Color.MediumBlue);
    public static Color MediumOrchid = FromBuiltin(System.Drawing.Color.MediumOrchid);
    public static Color MediumPurple = FromBuiltin(System.Drawing.Color.MediumPurple);
    public static Color MediumSeaGreen = FromBuiltin(System.Drawing.Color.MediumSeaGreen);
    public static Color MediumSlateBlue = FromBuiltin(System.Drawing.Color.MediumSlateBlue);
    public static Color MediumSpringGreen = FromBuiltin(System.Drawing.Color.MediumSpringGreen);
    public static Color MediumTurquoise = FromBuiltin(System.Drawing.Color.MediumTurquoise);
    public static Color MediumVioletRed = FromBuiltin(System.Drawing.Color.MediumVioletRed);
    public static Color MidnightBlue = FromBuiltin(System.Drawing.Color.MidnightBlue);
    public static Color MintCream = FromBuiltin(System.Drawing.Color.MintCream);
    public static Color MistyRose = FromBuiltin(System.Drawing.Color.MistyRose);
    public static Color Moccasin = FromBuiltin(System.Drawing.Color.Moccasin);
    public static Color NavajoWhite = FromBuiltin(System.Drawing.Color.NavajoWhite);
    public static Color Navy = FromBuiltin(System.Drawing.Color.Navy);
    public static Color OldLace = FromBuiltin(System.Drawing.Color.OldLace);
    public static Color Olive = FromBuiltin(System.Drawing.Color.Olive);
    public static Color OliveDrab = FromBuiltin(System.Drawing.Color.OliveDrab);
    public static Color Orange = FromBuiltin(System.Drawing.Color.Orange);
    public static Color OrangeRed = FromBuiltin(System.Drawing.Color.OrangeRed);
    public static Color Orchid = FromBuiltin(System.Drawing.Color.Orchid);
    public static Color PaleGoldenrod = FromBuiltin(System.Drawing.Color.PaleGoldenrod);
    public static Color PaleGreen = FromBuiltin(System.Drawing.Color.PaleGreen);
    public static Color PaleTurquoise = FromBuiltin(System.Drawing.Color.PaleTurquoise);
    public static Color PaleVioletRed = FromBuiltin(System.Drawing.Color.PaleVioletRed);
    public static Color PapayaWhip = FromBuiltin(System.Drawing.Color.PapayaWhip);
    public static Color PeachPuff = FromBuiltin(System.Drawing.Color.PeachPuff);
    public static Color Peru = FromBuiltin(System.Drawing.Color.Peru);
    public static Color Pink = FromBuiltin(System.Drawing.Color.Pink);
    public static Color Plum = FromBuiltin(System.Drawing.Color.Plum);
    public static Color PowderBlue = FromBuiltin(System.Drawing.Color.PowderBlue);
    public static Color Purple = FromBuiltin(System.Drawing.Color.Purple);
    public static Color RebeccaPurple = FromBuiltin(System.Drawing.Color.RebeccaPurple);
    public static Color Red = FromBuiltin(System.Drawing.Color.Red);
    public static Color RosyBrown = FromBuiltin(System.Drawing.Color.RosyBrown);
    public static Color RoyalBlue = FromBuiltin(System.Drawing.Color.RoyalBlue);
    public static Color SaddleBrown = FromBuiltin(System.Drawing.Color.SaddleBrown);
    public static Color Salmon = FromBuiltin(System.Drawing.Color.Salmon);
    public static Color SandyBrown = FromBuiltin(System.Drawing.Color.SandyBrown);
    public static Color SeaGreen = FromBuiltin(System.Drawing.Color.SeaGreen);
    public static Color SeaShell = FromBuiltin(System.Drawing.Color.SeaShell);
    public static Color Sienna = FromBuiltin(System.Drawing.Color.Sienna);
    public static Color Silver = FromBuiltin(System.Drawing.Color.Silver);
    public static Color SkyBlue = FromBuiltin(System.Drawing.Color.SkyBlue);
    public static Color SlateBlue = FromBuiltin(System.Drawing.Color.SlateBlue);
    public static Color SlateGray = FromBuiltin(System.Drawing.Color.SlateGray);
    public static Color Snow = FromBuiltin(System.Drawing.Color.Snow);
    public static Color SpringGreen = FromBuiltin(System.Drawing.Color.SpringGreen);
    public static Color SteelBlue = FromBuiltin(System.Drawing.Color.SteelBlue);
    public static Color Tan = FromBuiltin(System.Drawing.Color.Tan);
    public static Color Teal = FromBuiltin(System.Drawing.Color.Teal);
    public static Color Thistle = FromBuiltin(System.Drawing.Color.Thistle);
    public static Color Tomato = FromBuiltin(System.Drawing.Color.Tomato);
    public static Color Turquoise = FromBuiltin(System.Drawing.Color.Turquoise);
    public static Color Violet = FromBuiltin(System.Drawing.Color.Violet);
    public static Color Wheat = FromBuiltin(System.Drawing.Color.Wheat);
    public static Color White = FromBuiltin(System.Drawing.Color.White);
    public static Color WhiteSmoke = FromBuiltin(System.Drawing.Color.WhiteSmoke);
    public static Color Yellow = FromBuiltin(System.Drawing.Color.Yellow);
    public static Color YellowGreen = FromBuiltin(System.Drawing.Color.YellowGreen);
}
