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

    public TypeInstanceSimple(Token identifier, Uri file, IEnumerable<TypeInstance>? typeArguments = null) : base(file)
    {
        Identifier = identifier;
        TypeArguments = typeArguments?.ToImmutableArray();
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

    public static TypeInstanceSimple CreateAnonymous(string name, Uri file)
        => new(Token.CreateAnonymous(name), file);

    public static TypeInstanceSimple CreateAnonymous(string name, Uri file, IEnumerable<TypeInstance>? typeArguments)
        => new(Token.CreateAnonymous(name), file, typeArguments);

    public static TypeInstanceSimple CreateAnonymous(string name, Uri file, IEnumerable<Token>? typeArguments)
    {
        TypeInstance[]? genericTypesConverted;
        if (typeArguments == null)
        { genericTypesConverted = null; }
        else
        {
            Token[] genericTypesA = typeArguments.ToArray();
            genericTypesConverted = new TypeInstance[genericTypesA.Length];
            for (int i = 0; i < genericTypesA.Length; i++)
            {
                genericTypesConverted[i] = TypeInstanceSimple.CreateAnonymous(genericTypesA[i].Content, file);
            }
        }

        return new TypeInstanceSimple(Token.CreateAnonymous(name), file, genericTypesConverted);
    }

    public override string ToString()
    {
        if (TypeArguments is null) return Identifier.Content;
        return $"{Identifier.Content}<{string.Join<TypeInstance>(", ", TypeArguments)}>";
    }
    public override string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments)
    {
        string identifier = Identifier.Content;
        if (typeArguments.TryGetValue(Identifier.Content, out GeneralType? replaced))
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
