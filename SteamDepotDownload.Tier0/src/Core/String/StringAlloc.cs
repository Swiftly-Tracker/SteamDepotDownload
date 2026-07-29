using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamDepotDownload.Tier0.Core.String;

internal static class StringAlloc
{
    private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

    public static void CreateTempCString(string str, Action<nint> action)
    {
        var totalByteCount = Encoding.UTF8.GetByteCount(str) + 1;
        var stringBuffer = _pool.Rent(totalByteCount);

        _ = Encoding.UTF8.GetBytes(str.AsSpan(), stringBuffer.AsSpan());
        stringBuffer[totalByteCount - 1] = 0;

        unsafe
        {
            fixed (byte* cstr = stringBuffer)
            {
                action((nint)cstr);
            }
        }

        _pool.Return(stringBuffer);
    }

    public static T CreateTempCString<T>(string str, Func<nint, T> action)
    {
        var totalByteCount = Encoding.UTF8.GetByteCount(str) + 1;
        var stringBuffer = _pool.Rent(totalByteCount);

        _ = Encoding.UTF8.GetBytes(str.AsSpan(), stringBuffer.AsSpan());
        stringBuffer[totalByteCount - 1] = 0;

        unsafe
        {
            fixed (byte* cstr = stringBuffer)
            {
                var result = action((nint)cstr);
                _pool.Return(stringBuffer);
                return result;
            }
        }
    }

    public static string CreateStringFromCallback(int length, Action<nint> action)
    {
        var totalByteCount = length + 1;
        var stringBuffer = _pool.Rent(totalByteCount);

        unsafe
        {
            fixed (byte* cstr = stringBuffer)
            {
                action((nint)cstr);
                var returnString = Encoding.UTF8.GetString(cstr, totalByteCount - 1);
                _pool.Return(stringBuffer);
                return returnString;
            }
        }
    }

    public static string CreateStringFromPointer(nint cstrPtr, int length)
    {
        if (cstrPtr == 0 || length == 0) return "";

        return Marshal.PtrToStringUTF8(cstrPtr, length);
    }

    public static string CreateStringFromPointer(nint cstrPtr)
    {
        if (cstrPtr == 0) return "";
        return Marshal.PtrToStringUTF8(cstrPtr) ?? "(null)";
    }
}