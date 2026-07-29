namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed class DepotDownloadException : Exception
{
    public DepotDownloadException(string message) : base(message)
    {
    }

    public DepotDownloadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
