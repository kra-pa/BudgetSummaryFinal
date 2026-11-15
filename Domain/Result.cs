namespace BudgetSummaryFinal.Domain;

public sealed record DomainError
{
    public DomainError(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }

    public string Code { get; }
    public string Message { get; }
}

public abstract record Result<T>;
public sealed record Success<T> : Result<T>
{
    public Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public T Value { get; }
}

public sealed record Failure<T> : Result<T>
{
    public Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    public DomainError Error { get; }
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new Success<T>(value);
    public static Result<T> Fail<T>(string code, string message) => new Failure<T>(new DomainError(code, message));

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> transform) => result switch
    {
        Success<TIn> success => Ok(transform(success.Value)),
        Failure<TIn> failure => new Failure<TOut>(failure.Error),
        _ => throw new ArgumentOutOfRangeException(nameof(result), "Unknown Result case.")
    };

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next) => result switch
    {
        Success<TIn> success => next(success.Value),
        Failure<TIn> failure => new Failure<TOut>(failure.Error),
        _ => throw new ArgumentOutOfRangeException(nameof(result), "Unknown Result case.")
    };
}
