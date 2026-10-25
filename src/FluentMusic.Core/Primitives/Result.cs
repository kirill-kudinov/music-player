namespace FluentMusic.Core.Primitives;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

internal enum ResultState : byte
{
    Uninitialized = 0,
    Ok = 1,
    Fail = 2
}

public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly ResultState _state;
    private readonly Error _error;

    public bool IsOk
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == ResultState.Ok;
    }

    public bool IsError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state != ResultState.Ok;
    }

    public Error Error => _state switch
    {
        ResultState.Ok => Error.None,
        ResultState.Fail => _error,
        _ => ResultErrors.Uninitialized
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Result(in T value)
    {
        _value = value;
        _state = ResultState.Ok;
        _error = Error.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Result(in Error error)
    {
        _value = default;
        _error = error.IsNone ? ResultErrors.NullValue : error;
        _state = ResultState.Fail;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetOk([NotNullWhen(true)] out T? value)
    {
        value = _value;
        return IsOk;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T>(FailedResult failedResult) => new(in failedResult.Error);
}

public readonly record struct FailedResult(Error Error)
{
    internal readonly Error Error = Error;
}

public static class Result
{
    private static readonly Result<Unit> OkUnit = new(in Unit.Value);

    public static Task<Result<Unit>> OkTask { get; } = Task.FromResult(OkUnit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit> Ok() => OkUnit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FailedResult Fail(in Error error) => new(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Ok<T>(in T value) =>
        value is null
            ? new FailedResult(ResultErrors.NullValue)
            : new Result<T>(in value);
}