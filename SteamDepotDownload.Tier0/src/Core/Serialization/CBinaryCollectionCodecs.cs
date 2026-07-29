using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Tier0.Core.Serialization;

internal static class CBinaryWireSize
{
    internal static int Minimum(CBinaryWireType wire) => wire switch
    {
        CBinaryWireType.Fixed32 => 4,
        CBinaryWireType.Fixed64 => 8,
        _ => 1,
    };
}

internal static class CBinaryElement
{
    internal static void RequireNotNull<T>(T value, string container)
    {
        if (value is null)
        {
            throw new NotSupportedException(
                $"A null element in {container} cannot be written; the binary format only marks whole members as absent.");
        }
    }
}

internal sealed class CListCodec<TElement> : ICBinaryCodec<List<TElement>>
{
    private static ICBinaryCodec<TElement> Element => CBinaryCodec<TElement>.Instance;

    public CBinaryWireType Wire => CBinaryWireType.Array;

    public void Write(CBinaryWriter writer, List<TElement> value)
    {
        var element = Element;

        writer.WriteVarUInt((ulong)value.Count);
        writer.WriteWire(element.Wire);

        foreach (var item in value)
        {
            CBinaryElement.RequireNotNull(item, $"List<{typeof(TElement).Name}>");
            element.Write(writer, item);
        }
    }

    public List<TElement> Read(CBinaryReader reader)
    {
        var element = Element;
        var count = reader.ReadCount(CBinaryWireSize.Minimum(element.Wire));
        var wire = (CBinaryWireType)reader.ReadByteOrThrow();

        if (wire != element.Wire)
        {
            throw new BinaryFormatException(
                $"A List<{typeof(TElement).Name}> holds wire type {(byte)wire}, not the expected {(byte)element.Wire}.");
        }

        var result = new List<TElement>(Math.Min(count, 1024));

        for (var i = 0; i < count; i++)
        {
            result.Add(element.Read(reader));
        }

        return result;
    }
}

internal sealed class CArrayCodec<TElement> : ICBinaryCodec<TElement[]>
{
    private static ICBinaryCodec<TElement> Element => CBinaryCodec<TElement>.Instance;

    public CBinaryWireType Wire => CBinaryWireType.Array;

    public void Write(CBinaryWriter writer, TElement[] value)
    {
        var element = Element;

        writer.WriteVarUInt((ulong)value.Length);
        writer.WriteWire(element.Wire);

        foreach (var item in value)
        {
            CBinaryElement.RequireNotNull(item, $"{typeof(TElement).Name}[]");
            element.Write(writer, item);
        }
    }

    public TElement[] Read(CBinaryReader reader)
    {
        var element = Element;
        var count = reader.ReadCount(CBinaryWireSize.Minimum(element.Wire));
        var wire = (CBinaryWireType)reader.ReadByteOrThrow();

        if (wire != element.Wire)
        {
            throw new BinaryFormatException(
                $"A {typeof(TElement).Name}[] holds wire type {(byte)wire}, not the expected {(byte)element.Wire}.");
        }

        var result = new TElement[count];

        for (var i = 0; i < count; i++)
        {
            result[i] = element.Read(reader);
        }

        return result;
    }
}

internal sealed class CDictionaryCodec<TKey, TValue> : ICBinaryCodec<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    private static ICBinaryCodec<TKey> Key => CBinaryCodec<TKey>.Instance;

    private static ICBinaryCodec<TValue> Value => CBinaryCodec<TValue>.Instance;

    public CBinaryWireType Wire => CBinaryWireType.Map;

    public void Write(CBinaryWriter writer, Dictionary<TKey, TValue> value)
    {
        var key = Key;
        var item = Value;

        writer.WriteVarUInt((ulong)value.Count);
        writer.WriteWire(key.Wire);
        writer.WriteWire(item.Wire);

        foreach (var pair in value)
        {
            CBinaryElement.RequireNotNull(pair.Value, $"Dictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>");

            key.Write(writer, pair.Key);
            item.Write(writer, pair.Value);
        }
    }

    public Dictionary<TKey, TValue> Read(CBinaryReader reader)
    {
        var key = Key;
        var item = Value;

        var count = reader.ReadCount(
            CBinaryWireSize.Minimum(key.Wire) + CBinaryWireSize.Minimum(item.Wire));

        var keyWire = (CBinaryWireType)reader.ReadByteOrThrow();
        var valueWire = (CBinaryWireType)reader.ReadByteOrThrow();

        if (keyWire != key.Wire || valueWire != item.Wire)
        {
            throw new BinaryFormatException(
                $"A Dictionary<{typeof(TKey).Name}, {typeof(TValue).Name}> holds wire types " +
                $"{(byte)keyWire}/{(byte)valueWire}, not the expected {(byte)key.Wire}/{(byte)item.Wire}.");
        }

        var result = new Dictionary<TKey, TValue>(Math.Min(count, 1024));

        for (var i = 0; i < count; i++)
        {
            result[key.Read(reader)] = item.Read(reader);
        }

        return result;
    }
}

internal sealed class CContractCodec<T> : ICBinaryCodec<T>
{
    public CBinaryWireType Wire => CBinaryWireType.Object;

    public void Write(CBinaryWriter writer, T value) => CBinaryTypePlan<T>.Instance.Write(writer, value);

    public T Read(CBinaryReader reader) => CBinaryTypePlan<T>.Instance.Read(reader);
}
