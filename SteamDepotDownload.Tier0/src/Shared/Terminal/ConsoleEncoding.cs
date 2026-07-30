using System.Text;

namespace SteamDepotDownload.Tier0.Shared.Terminal;

public static class ConsoleEncoding
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void EnsureUtf8()
    {
        TrySetOutput(Utf8NoBom);

        if (!Console.IsInputRedirected)
        {
            TrySetInput(Utf8NoBom);
        }
    }

    public static void Restore(Encoding output) => TrySetOutput(output);

    private static void TrySetOutput(Encoding encoding)
    {
        try
        {
            Console.OutputEncoding = encoding;
        }
        catch (IOException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    private static void TrySetInput(Encoding encoding)
    {
        try
        {
            Console.InputEncoding = encoding;
        }
        catch (IOException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }
}
