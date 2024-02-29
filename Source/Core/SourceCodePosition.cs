namespace LanguageCore;

public interface IPositioned
{
    public Position Position { get; }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct Position :
    IEquatable<Position>,
    IEqualityOperators<Position, Position, bool>
{
    public static Position UnknownPosition => new(new Range<SinglePosition>(SinglePosition.Undefined), new Range<int>(-1));
    public static Position Zero => new(new Range<SinglePosition>(SinglePosition.Zero), new Range<int>(0));

    public readonly Range<int> AbsoluteRange;
    public readonly Range<SinglePosition> Range;

    public Position(Range<SinglePosition> range, Range<int> absoluteRange)
    {
        Range = range;
        AbsoluteRange = absoluteRange;
    }

    public Position(ValueTuple<SinglePosition, SinglePosition> range, ValueTuple<int, int> absoluteRange)
    {
        Range = range;
        AbsoluteRange = absoluteRange;
    }

    public Position(params IPositioned?[] elements)
    {
        if (elements.Length == 0) throw new ArgumentException($"Number of elements must be more than zero", nameof(elements));

        Range = Position.UnknownPosition.Range;
        AbsoluteRange = Position.UnknownPosition.AbsoluteRange;

        for (int i = 0; i < elements.Length; i++)
        {
            IPositioned? element = elements[i];
            if (element == null) continue;
            if (element is Tokenizing.Token token && token.IsAnonymous) continue;
            Position position = element.Position;
            Range = position.Range;
            AbsoluteRange = position.AbsoluteRange;
            break;
        }

        Position result = this;

        for (int i = 1; i < elements.Length; i++)
        { result = result.Union(elements[i]); }

        Range = result.Range;
        AbsoluteRange = result.AbsoluteRange;
    }
    public Position(IEnumerable<IPositioned?> elements) : this(elements.ToArray())
    { }

    public string ToStringRange()
    {
        if (Range.Start == Range.End) return Range.Start.ToStringMin();
        if (Range.Start.Line == Range.End.Line) return $"{Range.Start.Line}:({Range.Start.Character}-{Range.End.Character})";
        return $"{Range.Start.ToStringMin()}-{Range.End.ToStringMin()}";
    }

    public string? ToStringCool(string? prefix = null, string? postfix = null)
    {
        if (Range.Start.Line < 0)
        { return null; }

        if (this == Position.UnknownPosition)
        { return null; }

        if (Range.Start.Character < 0)
        { return $"{prefix}line {Range.Start.Character}{postfix}"; }

        return $"{prefix}line {Range.Start.Line} and column {Range.Start.Character}{postfix}";
    }

    string GetDebuggerDisplay()
    {
        if (this == Position.UnknownPosition)
        { return "?"; }
        return this.ToStringRange();
    }

    public Position Before() => new(new Range<SinglePosition>(new SinglePosition(this.Range.Start.Line, this.Range.Start.Character - 1), new SinglePosition(this.Range.Start.Line, this.Range.Start.Character)), new Range<int>(this.AbsoluteRange.Start - 1, this.AbsoluteRange.Start));

    public Position After() => new(new Range<SinglePosition>(new SinglePosition(this.Range.End.Line, this.Range.End.Character), new SinglePosition(this.Range.End.Line, this.Range.End.Character + 1)), new Range<int>(this.AbsoluteRange.End, this.AbsoluteRange.End + 1));

    public override bool Equals(object? obj) => obj is Position position && Equals(position);
    public bool Equals(Position other) => AbsoluteRange.Equals(other.AbsoluteRange) && Range.Equals(other.Range);

    public override int GetHashCode() => HashCode.Combine(AbsoluteRange, Range);

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public (Position Left, Position Right) Slice(int at)
    {
        if (Range.Start.Line != Range.End.Line)
        { throw new NotImplementedException($"Position slicing on different lines not implemented"); }

        ArgumentOutOfRangeException.ThrowIfNegative(at);
        int rangeSize = Range.End.Character - Range.Start.Character;

        if (rangeSize < 0)
        { throw new NotImplementedException($"Somehow end is larger than start"); }

        if (rangeSize < at)
        { throw new ArgumentOutOfRangeException(nameof(at), at, $"Slice location is larger than the range size ({rangeSize})"); }

        int rangeSizeAbs = AbsoluteRange.End - AbsoluteRange.Start;

        if (rangeSizeAbs < 0)
        { throw new NotImplementedException($"Somehow end is larger than start"); }

        if (rangeSizeAbs < at)
        { throw new ArgumentOutOfRangeException(nameof(at), at, $"Slice location is larger than the range size ({rangeSizeAbs})"); }

        Position left = new(
             new Range<SinglePosition>(
                Range.Start,
                new SinglePosition(Range.Start.Line, Range.Start.Character + at)
                ),
             new Range<int>(
                AbsoluteRange.Start,
                AbsoluteRange.Start + at
                )
            );

        Position right = new(
             new Range<SinglePosition>(
                left.Range.End,
                Range.End
                ),
             new Range<int>(
                left.AbsoluteRange.End,
                AbsoluteRange.End
                )
            );

        return (left, right);
    }

    public static bool operator ==(Position left, Position right) => left.Equals(right);
    public static bool operator !=(Position left, Position right) => !left.Equals(right);
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct SinglePosition :
    IEquatable<SinglePosition>,
    IComparisonOperators<SinglePosition, SinglePosition, bool>,
    IEqualityOperators<SinglePosition, SinglePosition, bool>,
    IMinMaxValue<SinglePosition>
{
    public int Line;
    public int Character;

    public readonly bool IsUndefined => Line < 0 || Character < 0;

    /// <summary> <c>(<see cref="int.MaxValue"/>, <see cref="int.MaxValue"/>)</c> </summary>
    public static SinglePosition MaxValue => new(int.MaxValue, int.MaxValue);
    /// <summary> <c>(0, 0)</c> </summary>
    public static SinglePosition MinValue => new(0, 0);
    /// <summary> <c>(-1, -1)</c> </summary>
    public static SinglePosition Undefined => new(-1, -1);
    /// <summary> <c>(0, 0)</c> </summary>
    public static SinglePosition Zero => new(0, 0);

    public SinglePosition(int line, int character)
    {
        Line = line;
        Character = character;
    }

    public static implicit operator SinglePosition(ValueTuple<int, int> v) => new(v.Item1, v.Item2);

    public static bool operator ==(SinglePosition a, SinglePosition b) => a.Line == b.Line && a.Character == b.Character;
    public static bool operator !=(SinglePosition a, SinglePosition b) => a.Line != b.Line || a.Character != b.Character;

    public static bool operator ==(SinglePosition a, int b) => a.Line == b && a.Character == b;
    public static bool operator !=(SinglePosition a, int b) => a.Line != b || a.Character != b;

    public static bool operator >(SinglePosition a, SinglePosition b)
    {
        if (a.Line > b.Line) return true;
        if (a.Character > b.Character && a.Line == b.Line) return true;
        return false;
    }

    public static bool operator <(SinglePosition a, SinglePosition b)
    {
        if (a.Line < b.Line) return true;
        if (a.Character < b.Character && a.Line == b.Line) return true;
        return false;
    }

    public static bool operator >=(SinglePosition a, SinglePosition b)
    {
        if (a.Line > b.Line) return true;
        if (a.Character >= b.Character && a.Line == b.Line) return true;
        return false;
    }

    public static bool operator <=(SinglePosition a, SinglePosition b)
    {
        if (a.Line < b.Line) return true;
        if (a.Character <= b.Character && a.Line == b.Line) return true;
        return false;
    }

    public override readonly string ToString() => $"({Line}:{Character})";
    public readonly string ToStringMin() => $"{Line}:{Character}";
    readonly string GetDebuggerDisplay()
    {
        if (this == SinglePosition.Undefined)
        { return "?"; }
        if (this == SinglePosition.Zero)
        { return "0"; }
        return ToString();
    }

    public override readonly bool Equals(object? obj) => obj is SinglePosition position && Equals(position);
    public readonly bool Equals(SinglePosition other) => Line == other.Line && Character == other.Character;
    public override readonly int GetHashCode() => HashCode.Combine(Line, Character);
}

public static class PositionExtensions
{
    public static Position Union(this Position a, Position b)
    {
        if (b == Position.UnknownPosition) return a;
        if (a == Position.UnknownPosition) return b;

        return new Position(
            Range.Union(a.Range, b.Range),
            Range.Union(a.AbsoluteRange, b.AbsoluteRange)
            );
    }

    public static Position Union(this Position a, IPositioned? b)
    {
        if (b is null) return a;

        if (b is Tokenizing.Token token && token.IsAnonymous) return a;
        return PositionExtensions.Union(a, b.Position);
    }

    public static Position Union(this Position a, params IPositioned?[]? b)
    {
        if (b is null) return a;

        Position result = a;

        for (int i = 0; i < b.Length; i++)
        { result = PositionExtensions.Union(result, b[i]); }

        return result;
    }

    public static Position Union(this Position a, IEnumerable<IPositioned?>? b)
    {
        if (b is null) return a;

        Position result = a;

        foreach (IPositioned? element in b)
        { result = PositionExtensions.Union(result, element); }

        return result;
    }
}
