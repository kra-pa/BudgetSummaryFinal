namespace BudgetSummaryFinal.Domain;

public abstract record Option<T>;
public sealed record Some<T> : Option<T>
{
    public Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public T Value { get; }
}
public sealed record None<T> : Option<T>;

public static class Option
{
    public static Result<T> ToResult<T>(this Option<T> option, DomainError error) => option switch
    {
        Some<T> some => Result.Ok(some.Value),
        None<T> => new Failure<T>(error),
        _ => throw new ArgumentOutOfRangeException(nameof(option), "Unknown Option case.")
    };
}
