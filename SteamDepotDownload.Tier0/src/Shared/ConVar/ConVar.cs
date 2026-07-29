using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.String;

namespace SteamDepotDownload.Tier0.Shared.ConVar;

public sealed class ConVar<T> : IConVar
{
    private T _value;

    public string Name { get; }
    public string? Description { get; }
    public ConVarFlags Flags { get; }
    public bool HasBounds { get; }
    public T Min { get; } = default!;
    public T Max { get; } = default!;

    public Type ValueType => typeof(T);

    public event Action<ConVar<T>, T, T>? OnChanged;

    public ConVar(string name, T defaultValue, string? description = null,
        ConVarFlags flags = ConVarFlags.None, (T Min, T Max)? bounds = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("ConVar name must not be empty.", nameof(name));
        }

        if (!ConVarTypes.IsSupported(typeof(T)))
        {
            throw new NotSupportedException($"ConVar type '{typeof(T)}' is not supported.");
        }

        if (bounds.HasValue && !ConVarTypes.IsNumeric(typeof(T)))
        {
            throw new ArgumentException($"Bounds are only valid for numeric ConVar types, not '{typeof(T)}'.", nameof(bounds));
        }

        Name = name;
        Description = description;
        Flags = flags;

        if (bounds.HasValue)
        {
            HasBounds = true;
            Min = bounds.Value.Min;
            Max = bounds.Value.Max;
        }

        _value = Clamp(defaultValue);

        ConVarRegistry.Register(this);
    }

    public T Value
    {
        get => _value;
        set => SetValue(value, force: false);
    }

    private void SetValue(T value, bool force)
    {
        if (!force && Flags.HasFlag(ConVarFlags.ReadOnly))
        {
            throw new InvalidOperationException($"ConVar '{Name}' is read-only and cannot be changed.");
        }

        T clamped = Clamp(value);
        if (EqualityComparer<T>.Default.Equals(_value, clamped))
        {
            return;
        }

        T old = _value;
        _value = clamped;

        OnChanged?.Invoke(this, old, clamped);
        ConVarRegistry.NotifyChanged(this);
    }

    public string ToStringValue() => _value?.ToString() ?? "";

    public void SetFromString(string value) => SetFromString(value, force: false);

    public void SetFromString(string value, bool force)
    {
        if (typeof(T) == typeof(string))
        {
            SetValue((T)(object)value, force);
            return;
        }

        if (typeof(T) == typeof(bool))
        {
            SetValue((T)(object)ParseBool(value, (bool)(object)_value!), force);
            return;
        }

        if (typeof(T).IsEnum)
        {
            bool isFlags = Attribute.IsDefined(typeof(T), typeof(FlagsAttribute));
            if (Enum.TryParse(typeof(T), value, ignoreCase: true, out var parsed)
                && parsed != null
                && (isFlags || Enum.IsDefined(typeof(T), parsed)))
            {
                SetValue((T)parsed, force);
            }
            return;
        }

        SetValue(Parse(value), force);
    }

    private static bool ParseBool(string s, bool current)
    {
        switch (s.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "on":
            case "yes":
                return true;
            case "0":
            case "false":
            case "off":
            case "no":
                return false;
            default:
                return current;
        }
    }

    private T Parse(string s)
    {
        var strConv = InterfaceSystem.GetInterface<IStrConv>(InterfaceNames.StrConv);
        if (strConv == null)
        {
            return _value;
        }

        object current = _value!;

        object result = typeof(T) switch
        {
            var t when t == typeof(byte) => strConv.Convert(s, (byte)current),
            var t when t == typeof(sbyte) => strConv.Convert(s, (sbyte)current),
            var t when t == typeof(short) => strConv.Convert(s, (short)current),
            var t when t == typeof(ushort) => strConv.Convert(s, (ushort)current),
            var t when t == typeof(int) => strConv.Convert(s, (int)current),
            var t when t == typeof(uint) => strConv.Convert(s, (uint)current),
            var t when t == typeof(long) => strConv.Convert(s, (long)current),
            var t when t == typeof(ulong) => strConv.Convert(s, (ulong)current),
            var t when t == typeof(float) => strConv.Convert(s, (float)current),
            var t when t == typeof(double) => strConv.Convert(s, (double)current),
            var t when t == typeof(bool) => strConv.Convert(s, (bool)current),
            _ => current,
        };

        return (T)result;
    }

    private T Clamp(T value)
    {
        if (!HasBounds)
        {
            return value;
        }

        var comparer = Comparer<T>.Default;
        if (comparer.Compare(value, Min) < 0)
        {
            return Min;
        }

        if (comparer.Compare(value, Max) > 0)
        {
            return Max;
        }

        return value;
    }
}
