namespace SteamDepotDownload.Tier0.Shared.Serialization;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class BinaryMemberAttribute : Attribute
{
    public int Tag { get; }

    public BinaryMemberAttribute(int tag)
    {
        if (tag <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tag), tag, "Binary member tags start at 1.");
        }

        Tag = tag;
    }
}
