namespace LanguageCore.Brainfuck;

public readonly struct PredictedNumber<T> :
    IAdditionOperators<PredictedNumber<T>, PredictedNumber<T>, PredictedNumber<T>>,
    ISubtractionOperators<PredictedNumber<T>, PredictedNumber<T>, PredictedNumber<T>>,
    IEquatable<PredictedNumber<T>>,
    IEquatable<T>
    where T : struct, INumberBase<T>
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsUnknown { get; }
    public T Value { get; }

    public static PredictedNumber<T> Unknown => new(true, default);

    public PredictedNumber(T value)
    {
        IsUnknown = false;
        Value = value;
    }

    PredictedNumber(bool isUnknown, T value)
    {
        IsUnknown = isUnknown;
        Value = value;
    }

    public static implicit operator PredictedNumber<T>(T value) => new(value);
    public static implicit operator T?(PredictedNumber<T> value) => value.IsUnknown ? default : value.Value;

    public static PredictedNumber<T> operator +(PredictedNumber<T> left, PredictedNumber<T> right)
        => (left.IsUnknown || right.IsUnknown) ? PredictedNumber<T>.Unknown : (left.Value + right.Value);
    public static PredictedNumber<T> operator -(PredictedNumber<T> left, PredictedNumber<T> right)
        => (left.IsUnknown || right.IsUnknown) ? PredictedNumber<T>.Unknown : (left.Value - right.Value);

    public static PredictedNumber<T> operator ++(PredictedNumber<T> left)
        => left.IsUnknown ? PredictedNumber<T>.Unknown : (left.Value + T.One);
    public static PredictedNumber<T> operator --(PredictedNumber<T> left)
        => left.IsUnknown ? PredictedNumber<T>.Unknown : (left.Value - T.One);

    public static bool operator ==(PredictedNumber<T> left, PredictedNumber<T> right) => left.Equals(right);
    public static bool operator !=(PredictedNumber<T> left, PredictedNumber<T> right) => !left.Equals(right);
    public static bool operator ==(PredictedNumber<T> left, T right) => left.Equals(right);
    public static bool operator !=(PredictedNumber<T> left, T right) => !left.Equals(right);

    public override bool Equals(object? obj) => obj is PredictedNumber<T> number && Equals(number);
    public bool Equals(PredictedNumber<T> other) => IsUnknown == other.IsUnknown && Value.Equals(other.Value);
    public bool Equals(T other) => !IsUnknown && Value.Equals(other);
    public override int GetHashCode() => HashCode.Combine(IsUnknown, Value);
    public override string? ToString() => IsUnknown ? "unknown" : Value.ToString();
}
