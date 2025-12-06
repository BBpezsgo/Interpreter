using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public class CompiledGenericTypeExpression : CompiledTypeExpression,
    IEquatable<CompiledGenericTypeExpression>,
    IReferenceableTo<Token>
{
    public string Identifier { get; }
    public Token? Definition { get; }
    public Uri File { get; }

    Token? IReferenceableTo<Token>.Reference
    {
        get => Definition;
        set => throw new InvalidOperationException();
    }

    [SetsRequiredMembers]
    public CompiledGenericTypeExpression(string identifier, Uri originalFile, Location location) : base(location)
    {
        Identifier = identifier;
        Definition = null;
        File = originalFile;
    }

    [SetsRequiredMembers]
    public CompiledGenericTypeExpression(Token definition, Uri originalFile, Location location) : base(location)
    {
        Identifier = definition.Content;
        Definition = definition;
        File = originalFile;
    }

    public override bool Equals(object? other) => Equals(other as CompiledGenericTypeExpression);
    public override bool Equals(CompiledTypeExpression? other) => Equals(other as CompiledGenericTypeExpression);
    public bool Equals(CompiledGenericTypeExpression? other)
    {
        if (other is null) return false;
        if (!Identifier.Equals(other.Identifier)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;

        if (other is not TypeInstanceSimple otherSimple)
        { return false; }

        if (TypeKeywords.BasicTypes.ContainsKey(otherSimple.Identifier.Content))
        { return false; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Identifier);
    public override string ToString() => Identifier;
    public override string Stringify(int depth = 0) => Identifier;

    public static CompiledGenericTypeExpression CreateAnonymous(GenericType type, ILocated location)
    {
        return new(
            type.Identifier,
            type.File,
            location.Location
        );
    }
}
