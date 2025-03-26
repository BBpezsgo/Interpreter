namespace LanguageCore;

public static class PositionExtensions
{
    public static Position Union(this Position a, Position b)
    {
        if (b == Position.UnknownPosition) return a;
        if (a == Position.UnknownPosition) return b;

        return new Position(
            RangeUtils.Union(a.Range, b.Range),
            RangeUtils.Union(a.AbsoluteRange, b.AbsoluteRange)
            );
    }

    public static Position Union(this Position a, params Position[] b)
    {
        if (b.Length == 0) return a;

        Position result = a;

        for (int i = 0; i < b.Length; i++)
        { result = PositionExtensions.Union(result, b[i]); }

        return result;
    }

    public static Position Union(this Position a, IPositioned? b)
    {
        if (b is null) return a;

        if (b is Tokenizing.Token token && token.IsAnonymous) return a;
        return PositionExtensions.Union(a, b.Position);
    }

    public static Position Union(this Position a, params IPositioned?[] b)
    {
        if (b.Length == 0) return a;

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
