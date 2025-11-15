using LanguageCore.Runtime;
using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class GenericType : GeneralType,
    IEquatable<GenericType>,
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

    public override bool GetSize(IRuntimeInfoProvider runtime, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = new PossibleDiagnostic($"Can not get the size of a generic type");
        size = default;
        return false;
    }

    public override bool GetBitWidth(IRuntimeInfoProvider runtime, out BitWidth bitWidth, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = new PossibleDiagnostic($"Can not get the size of a generic type");
        bitWidth = default;
        return false;
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
