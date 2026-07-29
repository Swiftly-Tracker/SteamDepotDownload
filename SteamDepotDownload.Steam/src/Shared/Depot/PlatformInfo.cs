using System.Runtime.InteropServices;

namespace SteamDepotDownload.Steam.Shared.Depot;

public static class PlatformInfo
{
    public const string Windows = "windows";
    public const string MacOs = "macos";
    public const string Linux = "linux";

    public static string HostOs
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Windows;
            }

            if (OperatingSystem.IsMacOS())
            {
                return MacOs;
            }

            return Linux;
        }
    }

    public static string HostArch => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X86 or Architecture.Arm => "32",
        _ => "64",
    };

    public static bool IsValidOs(string os)
        => os is Windows or MacOs or Linux;

    public static bool IsValidArch(string arch)
        => arch is "32" or "64";
}
