namespace LanguageCore.Brainfuck;

public readonly struct PredictedBrainfuckNumber
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsUnknown { get; }
    public byte Value { get; }

    public static PredictedBrainfuckNumber Unknown => new(true, default);

    public PredictedBrainfuckNumber(byte value)
    {
        IsUnknown = false;
        Value = value;
    }

    public PredictedBrainfuckNumber(int value)
    {
        IsUnknown = false;
        Value = (byte)value;
    }

    PredictedBrainfuckNumber(bool isUnknown, byte value)
    {
        IsUnknown = isUnknown;
        Value = value;
    }

    public static implicit operator PredictedBrainfuckNumber(byte value) => new(value);
    public static implicit operator byte?(PredictedBrainfuckNumber value) => value.IsUnknown ? default : value.Value;

    public static PredictedBrainfuckNumber operator +(PredictedBrainfuckNumber left, PredictedBrainfuckNumber right)
        => (left.IsUnknown || right.IsUnknown) ? Unknown : new(left.Value + right.Value);
    public static PredictedBrainfuckNumber operator -(PredictedBrainfuckNumber left, PredictedBrainfuckNumber right)
        => (left.IsUnknown || right.IsUnknown) ? Unknown : new(left.Value - right.Value);

    public static PredictedBrainfuckNumber operator ++(PredictedBrainfuckNumber left)
        => left.IsUnknown ? Unknown : new(left.Value + 1);
    public static PredictedBrainfuckNumber operator --(PredictedBrainfuckNumber left)
        => left.IsUnknown ? Unknown : new(left.Value - 1);

    public static bool operator ==(PredictedBrainfuckNumber left, PredictedBrainfuckNumber right) => left.Equals(right);
    public static bool operator !=(PredictedBrainfuckNumber left, PredictedBrainfuckNumber right) => !left.Equals(right);
    public static bool operator ==(PredictedBrainfuckNumber left, byte right) => left.Equals(right);
    public static bool operator !=(PredictedBrainfuckNumber left, byte right) => !left.Equals(right);

    public override bool Equals(object? obj) => obj is PredictedBrainfuckNumber number && Equals(number);
    public bool Equals(PredictedBrainfuckNumber other) => IsUnknown == other.IsUnknown && Value.Equals(other.Value);
    public bool Equals(byte other) => !IsUnknown && Value.Equals(other);
    public override int GetHashCode() => HashCode.Combine(IsUnknown, Value);
    public override string? ToString() => IsUnknown ? "unknown" : Value.ToString();
}
