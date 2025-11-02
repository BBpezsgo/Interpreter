using LanguageCore.Compiler;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public class TypeInstanceSimple : TypeInstance, IEquatable<TypeInstanceSimple?>, IReferenceableTo
{
    public Token Identifier { get; }
    public ImmutableArray<TypeInstance>? TypeArguments { get; }
    public object? Reference { get; set; }

    public override Position Position =>
        new Position(Identifier)
        .Union(TypeArguments);

    public TypeInstanceSimple(Token identifier, Uri file, ImmutableArray<TypeInstance>? typeArguments = null) : base(file)
    {
        Identifier = identifier;
        TypeArguments = typeArguments;
    }

    public override bool Equals(object? obj) => obj is TypeInstanceSimple other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstanceSimple other_ && Equals(other_);
    public bool Equals(TypeInstanceSimple? other)
    {
        if (other is null) return false;
        if (Identifier.Content != other.Identifier.Content) return false;

        if (!TypeArguments.HasValue) return other.TypeArguments is null;
        if (!other.TypeArguments.HasValue) return false;

        if (TypeArguments.Value.Length != other.TypeArguments.Value.Length) return false;
        for (int i = 0; i < TypeArguments.Value.Length; i++)
        {
            if (!TypeArguments.Value[i].Equals(other.TypeArguments.Value[i]))
            { return false; }
        }
        return true;
    }

    public override int GetHashCode() => HashCode.Combine((byte)3, Identifier, TypeArguments);

    public override void SetAnalyzedType(GeneralType type)
    {
        Identifier.AnalyzedType = type switch
        {
            StructType => TokenAnalyzedType.Struct,
            GenericType => TokenAnalyzedType.TypeParameter,
            BuiltinType => TokenAnalyzedType.BuiltinType,
            AliasType => TokenAnalyzedType.Type,
            _ => Identifier.AnalyzedType,
        };
    }

    public static TypeInstanceSimple CreateAnonymous(string name, Uri file, ImmutableArray<TypeInstance>? typeArguments = null)
        => new(Token.CreateAnonymous(name), file, typeArguments);

    public static TypeInstanceSimple CreateAnonymous(string name, Uri file, ImmutableArray<Token>? typeArguments)
    {
        TypeInstance[]? genericTypesConverted;
        if (typeArguments == null)
        { genericTypesConverted = null; }
        else
        {
            genericTypesConverted = new TypeInstance[typeArguments.Value.Length];
            for (int i = 0; i < typeArguments.Value.Length; i++)
            {
                genericTypesConverted[i] = TypeInstanceSimple.CreateAnonymous(typeArguments.Value[i].Content, file);
            }
        }

        return new TypeInstanceSimple(Token.CreateAnonymous(name), file, genericTypesConverted?.ToImmutableArray());
    }

    public override string ToString()
    {
        if (TypeArguments is null) return Identifier.Content;
        return $"{Identifier.Content}<{string.Join<TypeInstance>(", ", TypeArguments)}>";
    }
    public override string ToString(IReadOnlyDictionary<string, GeneralType>? typeArguments)
    {
        string identifier = Identifier.Content;
        if (typeArguments is not null && typeArguments.TryGetValue(Identifier.Content, out GeneralType? replaced))
        { identifier = replaced.ToString(); }

        if (!TypeArguments.HasValue)
        { return identifier; }

        StringBuilder result = new(identifier);
        result.Append('<');
        for (int i = 0; i < TypeArguments.Value.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(TypeArguments.Value[i].ToString(typeArguments));
        }
        result.Append('>');
        return result.ToString();
    }
}
