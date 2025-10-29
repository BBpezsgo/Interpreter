using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class TypeInstancePointer : TypeInstance, IEquatable<TypeInstancePointer?>
{
    public TypeInstance To { get; }
    public Token Operator { get; }

    public override Position Position => new(To, Operator);

    public TypeInstancePointer(TypeInstance to, Token @operator, Uri file) : base(file)
    {
        To = to;
        Operator = @operator;
    }

    public override bool Equals(object? obj) => obj is TypeInstancePointer other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstancePointer other_ && Equals(other_);
    public bool Equals(TypeInstancePointer? other)
    {
        if (other is null) return false;
        return this.To.Equals(other.To);
    }

    public override int GetHashCode() => HashCode.Combine((byte)4, To);

    public override void SetAnalyzedType(GeneralType type)
    {
        if (!type.Is(out PointerType? pointerType)) return;
        To.SetAnalyzedType(pointerType.To);
    }

    public override string ToString() => $"{To}{Operator}";
    public override string ToString(IReadOnlyDictionary<string, GeneralType>? typeArguments) => $"{To.ToString(typeArguments)}{Operator}";

    public static TypeInstancePointer CreateAnonymous(TypeInstance to, Uri file) => new(to, Token.CreateAnonymous("*", TokenType.Operator), file);
}
