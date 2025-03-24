using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public abstract class TypeInstance :
    IEquatable<TypeInstance>,
    IPositioned,
    IInFile,
    ILocated
{
    public abstract Position Position { get; }

    public Uri File { get; }
    public Location Location => new(Position, File);

    protected TypeInstance(Uri file)
    {
        File = file;
    }

    public static bool operator ==(TypeInstance? a, string? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a is not TypeInstanceSimple a2) return false;
        if (a2.TypeArguments is not null) return false;
        return a2.Identifier.Content == b;
    }
    public static bool operator !=(TypeInstance? a, string? b) => !(a == b);

    public static bool operator ==(string? a, TypeInstance? b) => b == a;
    public static bool operator !=(string? a, TypeInstance? b) => !(b == a);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is null) return false;
        if (obj is not TypeInstance other) return false;
        return this.Equals(other);
    }

    public abstract bool Equals(TypeInstance? other);

    public abstract override int GetHashCode();
    public abstract override string ToString();
    public virtual string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments) => ToString();

    protected static bool TryGetAnalyzedType(GeneralType type, out TokenAnalyzedType analyzedType)
    {
        analyzedType = default;

        switch (type.FinalValue)
        {
            case StructType:
                analyzedType = TokenAnalyzedType.Struct;
                return true;
            case GenericType:
                analyzedType = TokenAnalyzedType.TypeParameter;
                return true;
            case BuiltinType:
                analyzedType = TokenAnalyzedType.BuiltinType;
                return true;
            case AliasType:
                analyzedType = TokenAnalyzedType.Type;
                return true;
            case FunctionType functionType:
                return TryGetAnalyzedType(functionType.ReturnType, out analyzedType);
            default:
                return false;
        }
    }

    public abstract void SetAnalyzedType(GeneralType type);
}
