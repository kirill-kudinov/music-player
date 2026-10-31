namespace MusicPlayer.Core.Primitives;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

[SkipLocalsInit]
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    private static readonly Unit _value = new();

    public static ref readonly Unit Value => ref _value;

    public static Task<Unit> OkTask { get; } = Task.FromResult(_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Unit other) => true;

    public override bool Equals(object? obj) => obj is Unit;

    public override int GetHashCode() => 0;

    public int CompareTo(Unit other) => 0;

    public int CompareTo(object? obj) => 0;

    public static bool operator ==(Unit left, Unit right) => true;

    public static bool operator !=(Unit left, Unit right) => false;

    public override string ToString() => "()";
}