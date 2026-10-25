namespace FluentMusic.Core.Primitives;

using System.Runtime.CompilerServices;

public readonly record struct Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public string Code { get; } = Code ?? string.Empty;

    public string Message { get; } = Message ?? string.Empty;

    public bool IsNone
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => string.IsNullOrEmpty(Code);
    }
}