namespace LanguageCore.Compiler;

using Tokenizing;
using Parser;
using Runtime;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class GenericType : GeneralType,
    IEquatable<GenericType>,
    IReferenceableTo<Token>
{
    public string Identifier { get; }
    public Token? Definition { get; }
    public Uri File { get; }

    public override int Size
    {
        [DoesNotReturn]
        get => throw new InternalException($"Can not get the size of a generic type");
    }

    public override int SizeBytes
    {
        [DoesNotReturn]
        get => throw new InternalException($"Can not get the size of a generic type");
    }

    public override BitWidth BitWidth => throw new InvalidOperationException($"Can not get the size of a generic type");

    Token? IReferenceableTo<Token>.Reference
    {
        get => Definition;
        set => throw null!;
    }

    public GenericType(GenericType other)
    {
        Identifier = other.Identifier;
        Definition = other.Definition;
        File = other.File;
    }

    public GenericType(string identifier, Uri originalFile)
    {
        Identifier = identifier;
        Definition = null;
        File = originalFile;
    }

    public GenericType(Token definition, Uri originalFile)
    {
        Identifier = definition.Content;
        Definition = definition;
        File = originalFile;
    }

    public override bool Equals(object? other) => Equals(other as GenericType);
    public override bool Equals(GeneralType? other) => Equals(other as GenericType);
    public bool Equals(GenericType? other)
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
}
