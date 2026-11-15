namespace MusicPlayer.Core.Primitives;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly T? _value;

    public bool HasValue { get; }

    public bool IsNone
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !HasValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Option(in T value)
    {
        _value = value;
        HasValue = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetSome([NotNullWhen(true)] out T? value)
    {
        value = _value;
        return HasValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ValueOr(in T fallback) => HasValue ? _value! : fallback;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Option<T> other)
    {
        if (!HasValue && !other.HasValue)
            return true;

        if (HasValue != other.HasValue)
            return false;

        return EqualityComparer<T?>.Default.Equals(_value, other._value);
    }

    public override bool Equals(object? obj) => obj is Option<T> other && Equals(other);

    public override int GetHashCode() =>
        HasValue ? EqualityComparer<T?>.Default.GetHashCode(_value) : 0;

    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Option<T>(NoneToken _) => default;

    public override string ToString() => HasValue ? $"Some({_value})" : "None";
}

[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly record struct NoneToken;

public static class Option
{
    public static NoneToken None
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Option<T> Some<T>(in T? value) =>
        value is not null ? new Option<T>(in value) : Cache<T>.NoneInstance;

    private static class Cache<T>
    {
        public static readonly Option<T> NoneInstance = default;
    }
}