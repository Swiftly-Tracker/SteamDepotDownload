namespace SteamDepotDownload.Tier0.Core.Serialization;

/// <remarks>
/// Three bits, packed into the low end of every member key. It exists so a reader can walk past a
/// member it has never heard of, which is the whole basis of the format's forward compatibility.
/// </remarks>
internal enum CBinaryWireType : byte
{
    /// <summary>Length-prefixed integer, little-endian base-128.</summary>
    VarInt = 0,

    /// <summary>Exactly four bytes.</summary>
    Fixed32 = 1,

    /// <summary>Exactly eight bytes.</summary>
    Fixed64 = 2,

    /// <summary>Varint byte count followed by that many raw bytes.</summary>
    Bytes = 3,

    /// <summary>Nested member keys, closed by the zero key.</summary>
    Object = 4,

    /// <summary>Varint count, the element wire type, then that many elements.</summary>
    Array = 5,

    /// <summary>Varint count, the key wire type, the value wire type, then that many pairs.</summary>
    Map = 6,
}
