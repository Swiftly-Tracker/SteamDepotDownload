namespace SteamDepotDownload.Tier0.Shared.ConVar;

public static class ConVarTypes
{
    public static bool IsSupported(Type type)
        => type.IsEnum
        || type == typeof(byte)
        || type == typeof(sbyte)
        || type == typeof(short)
        || type == typeof(ushort)
        || type == typeof(int)
        || type == typeof(uint)
        || type == typeof(long)
        || type == typeof(ulong)
        || type == typeof(float)
        || type == typeof(double)
        || type == typeof(bool)
        || type == typeof(string);

    public static bool IsNumeric(Type type)
        => IsSupported(type) && !type.IsEnum && type != typeof(bool) && type != typeof(string);
}
