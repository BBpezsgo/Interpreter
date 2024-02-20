using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace LanguageCore.Brainfuck;

public readonly struct PredictedNumber<T> :
    IAdditionOperators<PredictedNumber<T>, PredictedNumber<T>, PredictedNumber<T>>,
    ISubtractionOperators<PredictedNumber<T>, PredictedNumber<T>, PredictedNumber<T>>,
    IEquatable<PredictedNumber<T>>,
    IEquatable<T>
    where T : struct, INumberBase<T>
{
    readonly bool _isUnknown;
    readonly T _value;

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsUnknown => _isUnknown;
    public T Value => _value;

    public static PredictedNumber<T> Unknown => new(true, default);

    public PredictedNumber(T value)
    {
        _isUnknown = false;
        _value = value;
    }

    PredictedNumber(bool isUnknown, T value)
    {
        _isUnknown = isUnknown;
        _value = value;
    }

    public static implicit operator PredictedNumber<T>(T value) => new(value);
    public static implicit operator T?(PredictedNumber<T> value) => value._isUnknown ? default : value._value;

    public static PredictedNumber<T> operator +(PredictedNumber<T> left, PredictedNumber<T> right)
        => (left._isUnknown || right._isUnknown) ? PredictedNumber<T>.Unknown : (left._value + right._value);
    public static PredictedNumber<T> operator -(PredictedNumber<T> left, PredictedNumber<T> right)
        => (left._isUnknown || right._isUnknown) ? PredictedNumber<T>.Unknown : (left._value - right._value);

    public static PredictedNumber<T> operator ++(PredictedNumber<T> left)
        => left._isUnknown ? PredictedNumber<T>.Unknown : (left._value + T.One);
    public static PredictedNumber<T> operator --(PredictedNumber<T> left)
        => left._isUnknown ? PredictedNumber<T>.Unknown : (left._value - T.One);

    public static bool operator ==(PredictedNumber<T> left, PredictedNumber<T> right) => left.Equals(right);
    public static bool operator !=(PredictedNumber<T> left, PredictedNumber<T> right) => !left.Equals(right);
    public static bool operator ==(PredictedNumber<T> left, T right) => left.Equals(right);
    public static bool operator !=(PredictedNumber<T> left, T right) => !left.Equals(right);

    public override bool Equals(object? obj) => obj is PredictedNumber<T> number && Equals(number);
    public bool Equals(PredictedNumber<T> other) => _isUnknown == other._isUnknown && _value.Equals(other._value);
    public bool Equals(T other) => _isUnknown == false && _value.Equals(other);
    public override int GetHashCode() => HashCode.Combine(_isUnknown, _value);
    public override string? ToString() => _isUnknown ? "unknown" : _value.ToString();
}
