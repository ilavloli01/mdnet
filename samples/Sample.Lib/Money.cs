namespace Core.Data;

/// <summary>An immutable amount of money in a specific currency.</summary>
/// <remarks>
/// Arithmetic is only defined for amounts in the same currency.
/// <list type="bullet">
/// <item><description>Amounts are rounded to 2 decimal places.</description></item>
/// <item><description>Currency codes follow ISO 4217.</description></item>
/// </list>
/// </remarks>
public readonly struct Money : IEquatable<Money>
{
    /// <summary>Creates a new amount.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The ISO 4217 currency code, e.g. <c>"EUR"</c>.</param>
    public Money(decimal amount, string currency = "EUR")
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets the amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the ISO 4217 currency code.</summary>
    public string Currency { get; init; }

    /// <summary>A zero amount in euros.</summary>
    public static readonly Money Zero = new(0m);

    /// <summary>Adds two amounts.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns>The sum of <paramref name="left"/> and <paramref name="right"/>.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public static Money operator +(Money left, Money right) =>
        left.Currency == right.Currency
            ? new(left.Amount + right.Amount, left.Currency)
            : throw new InvalidOperationException();

    /// <summary>Converts a decimal to an amount in euros.</summary>
    /// <param name="amount">The amount.</param>
    public static implicit operator Money(decimal amount) => new(amount);

    /// <inheritdoc />
    public bool Equals(Money other) => Amount == other.Amount && Currency == other.Currency;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
}

/// <summary>Extension methods for <see cref="Money"/>.</summary>
public static class MoneyExtensions
{
    /// <summary>Sums a sequence of amounts.</summary>
    /// <param name="source">The amounts to sum.</param>
    /// <returns>The total, or <see cref="Money.Zero"/> for an empty sequence.</returns>
    public static Money Sum(this IEnumerable<Money> source) => source.Aggregate(Money.Zero, (a, b) => a + b);

    /// <summary>Formats the amount for display.</summary>
    /// <typeparam name="TFormatter">The formatter type.</typeparam>
    /// <param name="money">The amount.</param>
    /// <param name="formatter">The formatter.</param>
    /// <returns>The formatted amount, e.g. <c>12.50 EUR</c>.</returns>
    public static string Format<TFormatter>(this Money money, TFormatter formatter)
        where TFormatter : IFormatProvider, new() =>
        money.Amount.ToString("0.00", formatter) + " " + money.Currency;
}
