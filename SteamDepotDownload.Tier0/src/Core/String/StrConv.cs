using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.String;

namespace SteamDepotDownload.Tier0.Core.String;

[ExposeInterface(InterfaceNames.StrConv)]
internal class CStrConv : IStrConv
{
    public T Convert<T>(string str, T defaultValue = default) where T : unmanaged
    {
        if (string.IsNullOrEmpty(str))
        {
            return defaultValue;
        }

        switch (defaultValue)
        {
            case byte:
                if (byte.TryParse(str, out var byteValue))
                {
                    return (T)(object)byteValue;
                }
                break;
            case sbyte:
                if (sbyte.TryParse(str, out var sbyteValue))
                {
                    return (T)(object)sbyteValue;
                }
                break;
            case short:
                if (short.TryParse(str, out var shortValue))
                {
                    return (T)(object)shortValue;
                }
                break;
            case ushort:
                if (ushort.TryParse(str, out var ushortValue))
                {
                    return (T)(object)ushortValue;
                }
                break;
            case int:
                if (int.TryParse(str, out var intValue))
                {
                    return (T)(object)intValue;
                }
                break;
            case uint:
                if (uint.TryParse(str, out var uintValue))
                {
                    return (T)(object)uintValue;
                }
                break;
            case long:
                if (long.TryParse(str, out var longValue))
                {
                    return (T)(object)longValue;
                }
                break;
            case ulong:
                if (ulong.TryParse(str, out var ulongValue))
                {
                    return (T)(object)ulongValue;
                }
                break;
            case float:
                if (float.TryParse(str, out var floatValue))
                {
                    return (T)(object)floatValue;
                }
                break;
            case double:
                if (double.TryParse(str, out var doubleValue))
                {
                    return (T)(object)doubleValue;
                }
                break;
            case decimal:
                if (decimal.TryParse(str, out var decimalValue))
                {
                    return (T)(object)decimalValue;
                }
                break;
            case char:
                if (char.TryParse(str, out var charValue))
                {
                    return (T)(object)charValue;
                }
                break;
            case bool:
                if (bool.TryParse(str, out var boolValue))
                {
                    return (T)(object)boolValue;
                }
                break;
            default:
                throw new NotSupportedException($"Type {typeof(T)} is not supported for conversion.");
        }

        return defaultValue;
    }

    public string Convert<T>(T value) where T : unmanaged
    {
        return value.ToString() ?? "<null>";
    }
}