using System.Linq.Expressions;
using System.Reflection;
using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Tier0.Core.Serialization;

internal sealed class CBinaryTypePlan<T>
{
    private static readonly Lazy<CBinaryTypePlan<T>> Lazy = new(Build);

    private readonly Func<T> _create;
    private readonly Action<CBinaryWriter, T> _write;
    private readonly Dictionary<int, CMember> _members;

    private CBinaryTypePlan(Func<T> create, Action<CBinaryWriter, T> write, Dictionary<int, CMember> members)
    {
        _create = create;
        _write = write;
        _members = members;
    }

    internal static CBinaryTypePlan<T> Instance => Lazy.Value;

    internal void Write(CBinaryWriter writer, T value) => _write(writer, value);

    internal T Read(CBinaryReader reader)
    {
        reader.Enter();

        var instance = _create();

        while (reader.ReadKey(out var tag, out var wire))
        {
            if (!_members.TryGetValue(tag, out var member))
            {
                reader.Skip(wire);
                continue;
            }

            if (member.Wire != wire)
            {
                throw new BinaryFormatException(
                    $"Tag {tag} on '{typeof(T)}' is wire type {(byte)member.Wire} but the payload holds " +
                    $"{(byte)wire}; the member's type changed without its tag being retired.");
            }

            member.Read(ref instance, reader);
        }

        reader.Leave();

        return instance;
    }

    private static CBinaryTypePlan<T> Build()
    {
        var type = typeof(T);

        if (!Attribute.IsDefined(type, typeof(BinaryContractAttribute), inherit: false))
        {
            throw new NotSupportedException($"'{type}' is not marked with [BinaryContract].");
        }

        var writer = Expression.Parameter(typeof(CBinaryWriter), "writer");
        var value = Expression.Parameter(type, "value");

        var writes = new List<Expression>();
        var members = new Dictionary<int, CMember>();

        foreach (var (member, tag) in Discover(type))
        {
            var memberType = member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;

            var underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;

            var codec = CBinaryCodecFactory.Create(underlying);
            var codecType = typeof(ICBinaryCodec<>).MakeGenericType(underlying);
            var codecConstant = Expression.Constant(codec, codecType);

            var wire = (CBinaryWireType)codecType.GetProperty(nameof(ICBinaryCodec<int>.Wire))!
                .GetValue(codec)!;

            if (members.ContainsKey(tag))
            {
                throw new InvalidOperationException($"'{type}' uses tag {tag} more than once.");
            }

            writes.Add(EmitWrite(writer, value, member, memberType, underlying, codecType, codecConstant, tag, wire));

            members[tag] = new CMember(wire,
                EmitRead(member, memberType, underlying, codecType, codecConstant));
        }

        writes.Add(Expression.Call(writer, Method(nameof(CBinaryWriter.WriteEndObject))));

        var write = Expression.Lambda<Action<CBinaryWriter, T>>(Expression.Block(writes), writer, value).Compile();

        return new CBinaryTypePlan<T>(BuildFactory(type), write, members);
    }

    private static Expression EmitWrite(ParameterExpression writer, ParameterExpression value,
        MemberInfo member, Type memberType, Type underlying, Type codecType, Expression codec,
        int tag, CBinaryWireType wire)
    {
        var access = Expression.MakeMemberAccess(value, member);

        var payload = Expression.Block(
            Expression.Call(writer, Method(nameof(CBinaryWriter.WriteKey)),
                Expression.Constant(tag), Expression.Constant(wire)),
            Expression.Call(codec, codecType.GetMethod(nameof(ICBinaryCodec<int>.Write))!,
                writer,
                underlying == memberType ? access : Expression.Property(access, "Value")));

        if (underlying != memberType)
        {
            return Expression.IfThen(Expression.Property(access, "HasValue"), payload);
        }

        return memberType.IsValueType
            ? payload
            : Expression.IfThen(Expression.ReferenceNotEqual(access, Expression.Constant(null, memberType)), payload);
    }

    private static CBinaryRefReader<T> EmitRead(MemberInfo member, Type memberType, Type underlying,
        Type codecType, Expression codec)
    {
        var instance = Expression.Parameter(typeof(T).MakeByRefType(), "instance");
        var reader = Expression.Parameter(typeof(CBinaryReader), "reader");

        Expression read = Expression.Call(codec, codecType.GetMethod(nameof(ICBinaryCodec<int>.Read))!, reader);

        if (underlying != memberType)
        {
            read = Expression.Convert(read, memberType);
        }

        var assign = Expression.Assign(Expression.MakeMemberAccess(instance, member), read);

        return Expression.Lambda<CBinaryRefReader<T>>(assign, instance, reader).Compile();
    }

    private static Func<T> BuildFactory(Type type)
    {
        if (type.IsValueType)
        {
            return static () => default!;
        }

        var constructor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);

        if (constructor == null)
        {
            throw new NotSupportedException(
                $"'{type}' needs a parameterless constructor to be read back.");
        }

        return Expression.Lambda<Func<T>>(Expression.New(constructor)).Compile();
    }

    private static IEnumerable<(MemberInfo Member, int Tag)> Discover(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var member in type.GetProperties(flags).Cast<MemberInfo>().Concat(type.GetFields(flags)))
        {
            if (member.GetCustomAttribute<BinaryMemberAttribute>(inherit: false) is not { } attribute)
            {
                continue;
            }

            if (member is PropertyInfo property && (!property.CanRead || !property.CanWrite))
            {
                throw new NotSupportedException(
                    $"'{type}.{property.Name}' carries [BinaryMember] but is not both readable and writable.");
            }

            if (member is FieldInfo { IsInitOnly: true } field)
            {
                throw new NotSupportedException(
                    $"'{type}.{field.Name}' carries [BinaryMember] but is readonly.");
            }

            yield return (member, attribute.Tag);
        }
    }

    private static MethodInfo Method(string name)
        => typeof(CBinaryWriter).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed record CMember(CBinaryWireType Wire, CBinaryRefReader<T> Read);
}
