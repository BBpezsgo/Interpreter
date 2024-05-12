namespace LanguageCore;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct SinglePosition :
    IEquatable<SinglePosition>,
    IComparisonOperators<SinglePosition, SinglePosition, bool>,
    IMinMaxValue<SinglePosition>,
    IComparable<SinglePosition>,
    IComparable
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
    public readonly int CompareTo(SinglePosition other)
    {
        if (Line < other.Line) return -1;
        if (Line > other.Line) return 1;

        if (Character < other.Character) return -1;
        if (Character > other.Character) return 1;

        return 0;
    }
    public readonly int CompareTo(object? obj) => obj is SinglePosition other ? CompareTo(other) : 0;
}
