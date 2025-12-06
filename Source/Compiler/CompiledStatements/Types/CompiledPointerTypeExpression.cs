using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledPointerTypeExpression : CompiledTypeExpression,
    IEquatable<CompiledPointerTypeExpression>
{
    public CompiledTypeExpression To { get; }

    [SetsRequiredMembers]
    public CompiledPointerTypeExpression(CompiledTypeExpression to, Location location) : base(location)
    {
        To = to;
    }

    public override bool Equals(object? other) => Equals(other as CompiledPointerTypeExpression);
    public override bool Equals(CompiledTypeExpression? other) => Equals(other as CompiledPointerTypeExpression);
    public bool Equals(CompiledPointerTypeExpression? other)
    {
        if (other is null) return false;
        if (!To.Equals(other.To)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstancePointer otherPointer) return false;
        if (!To.Equals(otherPointer.To)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(To);
    public override string ToString() => $"{To}*";
    public override string Stringify(int depth = 0) => $"{To.Stringify(depth)}*";

    public static CompiledPointerTypeExpression CreateAnonymous(PointerType type, ILocated location)
    {
        return new(CreateAnonymous(type.To, location), location.Location);
    }
}
