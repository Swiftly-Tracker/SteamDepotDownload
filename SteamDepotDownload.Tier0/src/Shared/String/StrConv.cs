namespace SteamDepotDownload.Tier0.Shared.String;

public interface IStrConv
{
    public T Convert<T>(string str, T defaultValue = default) where T : unmanaged;
    public string Convert<T>(T value) where T : unmanaged;
}