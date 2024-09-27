﻿namespace LanguageCore;

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

#if NET_STANDARD
    public Position(params IPositioned?[] elements)
#else
    public Position(params ReadOnlySpan<IPositioned?> elements)
#endif
    {
        if (elements.Length == 0) throw new ArgumentException($"Number of elements must be more than zero", nameof(elements));

        Range = Position.UnknownPosition.Range;
        AbsoluteRange = Position.UnknownPosition.AbsoluteRange;

        for (int i = 0; i < elements.Length; i++)
        {
            IPositioned? element = elements[i];
            if (element is null) continue;
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
    public Position(IPositioned item1)
    {
        Range = Position.UnknownPosition.Range;
        AbsoluteRange = Position.UnknownPosition.AbsoluteRange;

        if (item1 is null || (item1 is Tokenizing.Token token && token.IsAnonymous)) return;

        Position position = item1.Position;
        Range = position.Range;
        AbsoluteRange = position.AbsoluteRange;
    }
#if NET_STANDARD
    public Position(IEnumerable<IPositioned?> elements) : this(elements.ToArray()) { }
#else
    public Position(IEnumerable<IPositioned?> elements) : this(elements.ToArray().AsSpan()) { }
#endif

    public string ToStringRange()
    {
        if (Range.Start == Range.End) return Range.Start.ToStringMin();
        if (Range.Start.Line == Range.End.Line) return $"{Range.Start.Line}:({Range.Start.Character}-{Range.End.Character})";
        return $"{Range.Start.ToStringMin()}-{Range.End.ToStringMin()}";
    }

    public string? ToStringCool()
    {
        if (Range.Start.Line < 0 ||
            this == Position.UnknownPosition)
        { return null; }

        if (Range.Start.Character < 0)
        { return $"line {Range.Start.Character}"; }

        return $"line {Range.Start.Line} and column {Range.Start.Character}";
    }

    string GetDebuggerDisplay()
    {
        if (this == Position.UnknownPosition)
        { return "?"; }
        return this.ToStringRange();
    }

    public Position Before() => new(new Range<SinglePosition>(new SinglePosition(this.Range.Start.Line, this.Range.Start.Character - 1), new SinglePosition(this.Range.Start.Line, this.Range.Start.Character)), new Range<int>(this.AbsoluteRange.Start - 1, this.AbsoluteRange.Start));

    public Position After() => new(new Range<SinglePosition>(new SinglePosition(this.Range.End.Line, this.Range.End.Character), new SinglePosition(this.Range.End.Line, this.Range.End.Character + 1)), new Range<int>(this.AbsoluteRange.End, this.AbsoluteRange.End + 1));

    public Position NextLine() => new(new Range<SinglePosition>(new SinglePosition(Range.End.Line + 1, 0), new SinglePosition(Range.End.Line + 1, 1)), new Range<int>(AbsoluteRange.End, AbsoluteRange.End + 1));

    public override bool Equals(object? obj) => obj is Position position && Equals(position);
    public bool Equals(Position other) => AbsoluteRange.Equals(other.AbsoluteRange) && Range.Equals(other.Range);

    public override int GetHashCode() => HashCode.Combine(AbsoluteRange, Range);

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public (Position Left, Position Right) Slice(int at)
    {
        if (Range.Start.Line != Range.End.Line)
        { throw new NotImplementedException($"Position slicing on different lines not implemented"); }

#if NET_STANDARD
        if (at < 0) throw new ArgumentOutOfRangeException(nameof(at));
#else
        ArgumentOutOfRangeException.ThrowIfNegative(at);
#endif
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
