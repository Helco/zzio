namespace zzre;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public readonly unsafe ref struct OptionalReference<TValue>
{
    private readonly ref TValue value;

    public OptionalReference()
    {
        value = ref Unsafe.NullRef<TValue>();
    }

    public OptionalReference(ref TValue valueRef)
    {
        value = ref valueRef;
    }

    public bool HasValue => !Unsafe.IsNullRef(ref value);

    public ref TValue Value
    {
        get
        {
            if (!HasValue)
                throw new ArgumentNullException("this", "Optional reference does not have a value");
            return ref value;
        }
    }

    public TValue GetValueOrDefault() => HasValue ? value : default!;
    public TValue GetValueOrDefault(TValue def) => HasValue ? value : def;

    public bool TrySetValue(in TValue newValue)
    {
        if (HasValue)
        {
            value = newValue;
            return true;
        }
        return false;
    }

    public static bool operator ==(OptionalReference<TValue> optRef, in TValue value) =>
        optRef.HasValue && EqualityComparer<TValue>.Default.Equals(optRef.value, value);

    public static bool operator !=(OptionalReference<TValue> optRef, in TValue value) =>
        !optRef.HasValue || !EqualityComparer<TValue>.Default.Equals(optRef.value, value);

    public static bool operator ==(in TValue value, OptionalReference<TValue> optRef) =>
        optRef.HasValue && EqualityComparer<TValue>.Default.Equals(optRef.value, value);

    public static bool operator !=(in TValue value, OptionalReference<TValue> optRef) =>
        !optRef.HasValue || !EqualityComparer<TValue>.Default.Equals(optRef.value, value);

    public static bool operator ==(OptionalReference<TValue> optRefA, OptionalReference<TValue> optRefB) =>
        optRefA.HasValue == optRefB.HasValue && (!optRefA.HasValue ||
        EqualityComparer<TValue>.Default.Equals(optRefA.value, optRefB.value));

    public static bool operator !=(OptionalReference<TValue> optRefA, OptionalReference<TValue> optRefB) =>
        optRefA.HasValue != optRefB.HasValue || (optRefA.HasValue &&
        !EqualityComparer<TValue>.Default.Equals(optRefA.value, optRefB.value));

    public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
    {
        null => !HasValue,
        TValue value => this == value,
        _ => false
    };

    public override int GetHashCode() => HasValue ? value!.GetHashCode() : 0;
}
