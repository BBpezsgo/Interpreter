namespace LanguageCore;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct Position :
    IEquatable<Position>,
    IEqualityOperators<Position, Position, bool>
{
    public static Position UnknownPosition => new(new Range<SinglePosition>(SinglePosition.Undefined), new Range<int>(-1));
    public static Position Zero => new(new Range<SinglePosition>(SinglePosition.Zero), new Range<int>(0));

    public readonly Range<int> AbsoluteRange;
    public readonly Range<SinglePosition> Range;

    public Position this[Range range] => Slice(range);

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
    public Position(IEnumerable<IPositioned?> elements) : this(elements.ToArray()) { }

    public string ToStringRange()
    {
        if (Range.Start == Range.End) return Range.Start.ToStringMin();
        if (Range.Start.Line == Range.End.Line) return $"{Range.Start.Line + 1}:({Range.Start.Character}-{Range.End.Character})";
        return $"{Range.Start.ToStringMin()}-{Range.End.ToStringMin()}";
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

    public (Position Left, Position Right) Cut(int at)
    {
        if (Range.Start.Line != Range.End.Line)
        { throw new NotImplementedException($"Position slicing on different lines not implemented"); }

        if (at < 0) throw new ArgumentOutOfRangeException(nameof(at));
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

    public Position Slice(Range range)
    {
        if (Range.Start.Line != Range.End.Line)
        { throw new InvalidOperationException($"The position is on multiple lines"); }

        int absoluteLength = AbsoluteRange.End - AbsoluteRange.Start;

        (int start, int length) = range.GetOffsetAndLength(absoluteLength);

        return Slice(start, length);
    }

    public Position Slice(int start, int length)
    {
        if (Range.Start.Line != Range.End.Line)
        { throw new InvalidOperationException($"The position is on multiple lines"); }

        int absoluteStart = AbsoluteRange.Start + start;
        int columnStart = Range.Start.Character + start;
        int absoluteEnd = absoluteStart + length;
        int columnEnd = absoluteEnd + length;

        return new Position(
            new Range<SinglePosition>(
                new SinglePosition(Range.Start.Line, columnStart),
                new SinglePosition(Range.End.Line, columnEnd)
            ),
            new Range<int>(
                absoluteStart,
                absoluteEnd
            )
        );
    }

    public static bool operator ==(Position left, Position right) => left.Equals(right);
    public static bool operator !=(Position left, Position right) => !left.Equals(right);
}
