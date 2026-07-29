using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Tier0.Core.Serialization;

internal static class CBinaryCodec<T>
{
    internal static readonly ICBinaryCodec<T> Instance = CBinaryCodecFactory.Create<T>();
}

internal static class CBinaryCodecFactory
{
    private static readonly Dictionary<Type, object> Scalars = new()
    {
        [typeof(bool)] = new CBoolCodec(),
        [typeof(byte)] = new CByteCodec(),
        [typeof(sbyte)] = new CSByteCodec(),
        [typeof(short)] = new CInt16Codec(),
        [typeof(ushort)] = new CUInt16Codec(),
        [typeof(int)] = new CInt32Codec(),
        [typeof(uint)] = new CUInt32Codec(),
        [typeof(long)] = new CInt64Codec(),
        [typeof(ulong)] = new CUInt64Codec(),
        [typeof(char)] = new CCharCodec(),
        [typeof(float)] = new CSingleCodec(),
        [typeof(double)] = new CDoubleCodec(),
        [typeof(decimal)] = new CDecimalCodec(),
        [typeof(string)] = new CStringCodec(),
        [typeof(byte[])] = new CByteArrayCodec(),
        [typeof(Guid)] = new CGuidCodec(),
        [typeof(TimeSpan)] = new CTimeSpanCodec(),
        [typeof(DateTime)] = new CDateTimeCodec(),
        [typeof(DateTimeOffset)] = new CDateTimeOffsetCodec(),
    };

    internal static ICBinaryCodec<T> Create<T>() => (ICBinaryCodec<T>)Create(typeof(T));

    internal static object Create(Type type)
    {
        if (Scalars.TryGetValue(type, out var scalar))
        {
            return scalar;
        }

        if (type.IsEnum)
        {
            return Instantiate(typeof(CEnumCodec<>), type);
        }

        if (Nullable.GetUnderlyingType(type) != null)
        {
            throw new NotSupportedException(
                $"'{type}' is only supported directly on a member, not nested inside a collection or another nullable.");
        }

        if (type.IsArray)
        {
            if (type.GetArrayRank() != 1)
            {
                throw new NotSupportedException($"'{type}' is multi-dimensional; only single-rank arrays are supported.");
            }

            return Instantiate(typeof(CArrayCodec<>), type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();

            if (definition == typeof(List<>))
            {
                return Instantiate(typeof(CListCodec<>), arguments[0]);
            }

            if (definition == typeof(Dictionary<,>))
            {
                return Instantiate(typeof(CDictionaryCodec<,>), arguments);
            }
        }

        if (Attribute.IsDefined(type, typeof(BinaryContractAttribute), inherit: false))
        {
            return Instantiate(typeof(CContractCodec<>), type);
        }

        throw new NotSupportedException(
            $"'{type}' has no binary codec. Mark it with [BinaryContract] or use a supported type.");
    }

    private static object Instantiate(Type definition, params Type[] arguments)
        => Activator.CreateInstance(definition.MakeGenericType(arguments))!;
}
