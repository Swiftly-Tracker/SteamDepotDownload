namespace SteamDepotDownload.Tier0.Shared.Serialization;

public sealed class BinaryFormatException : Exception
{
    public BinaryFormatException(string message) : base(message)
    {
    }

    public BinaryFormatException(string message, Exception inner) : base(message, inner)
    {
    }
}
