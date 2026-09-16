using System.Diagnostics.CodeAnalysis;

namespace Inkoova.Academy.Domain.Common;

/// <summary>
/// Explicit success/failure for every domain and application operation.
/// Exceptions are reserved for infrastructure faults (CLAUDE.md convention 3).
/// </summary>
public readonly struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(TValue value)
    {
        _value = value;
        _error = default;
        IsSuccess = true;
    }

    private Result(TError error, bool _)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value of a failed Result. Error: {_error}");

    public TError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot read Error of a successful Result.");

    public static Result<TValue, TError> Success(TValue value) => new(value);

    public static Result<TValue, TError> Failure(TError error) => new(error, false);

    public static implicit operator Result<TValue, TError>(TValue value) => Success(value);

    public static implicit operator Result<TValue, TError>(TError error) => Failure(error);

    public bool TryGet([NotNullWhen(true)] out TValue? value, [NotNullWhen(false)] out TError? error)
    {
        value = _value;
        error = _error;
        return IsSuccess;
    }

    public Result<TNext, TError> Map<TNext>(Func<TValue, TNext> map) =>
        IsSuccess
            ? Result<TNext, TError>.Success(map(_value!))
            : Result<TNext, TError>.Failure(_error!);

    public Result<TNext, TError> Bind<TNext>(Func<TValue, Result<TNext, TError>> bind) =>
        IsSuccess ? bind(_value!) : Result<TNext, TError>.Failure(_error!);

    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<TError, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}

/// <summary>Shorthand for the only error type the domain uses.</summary>
public static class Result
{
    public static Result<TValue, Error> Ok<TValue>(TValue value) => Result<TValue, Error>.Success(value);

    public static Result<TValue, Error> Fail<TValue>(Error error) => Result<TValue, Error>.Failure(error);
}

/// <summary>Result of an operation that returns nothing meaningful on success.</summary>
public readonly struct Unit
{
    public static readonly Unit Value;
}
