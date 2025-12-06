using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledStructTypeExpression : CompiledTypeExpression,
    IEquatable<CompiledStructTypeExpression>,
    IReferenceableTo<CompiledStruct>
{
    public CompiledStruct Struct { get; }
    public ImmutableDictionary<string, CompiledTypeExpression> TypeArguments { get; }
    public Uri File { get; }

    CompiledStruct? IReferenceableTo<CompiledStruct>.Reference
    {
        get => Struct;
        set => throw new InvalidOperationException();
    }

    [SetsRequiredMembers]
    public CompiledStructTypeExpression(CompiledStructTypeExpression other) : base(other.Location)
    {
        Struct = other.Struct;
        TypeArguments = other.TypeArguments;
        File = other.File;
    }

    [SetsRequiredMembers]
    public CompiledStructTypeExpression(CompiledStruct @struct, Uri originalFile, Location location) : base(location)
    {
        Struct = @struct;
        if (@struct.Template is not null)
        {
            TypeArguments = @struct.Template.Parameters
                .Select(v => new KeyValuePair<string, CompiledTypeExpression>(v.Content, new CompiledGenericTypeExpression(v, originalFile, location)))
                .ToImmutableDictionary();
        }
        else
        { TypeArguments = ImmutableDictionary<string, CompiledTypeExpression>.Empty; }
        File = originalFile;
    }

    [SetsRequiredMembers]
    public CompiledStructTypeExpression(CompiledStruct @struct, Uri originalFile, IReadOnlyList<CompiledTypeExpression> typeArguments, Location location) : base(location)
    {
        Struct = @struct;
        if (@struct.Template is not null)
        {
            Dictionary<string, CompiledTypeExpression> result = new(@struct.Template.Parameters.Length);

            if (@struct.Template.Parameters.Length != typeArguments.Count)
            { throw new InternalExceptionWithoutContext("Length of type parameters doesn't matching with length of type arguments"); }

            for (int i = 0; i < @struct.Template.Parameters.Length; i++)
            { result.Add(@struct.Template.Parameters[i].Content, typeArguments[i]); }

            TypeArguments = result.ToImmutableDictionary();
        }
        else
        { TypeArguments = ImmutableDictionary<string, CompiledTypeExpression>.Empty; }
        File = originalFile;
    }

    [SetsRequiredMembers]
    public CompiledStructTypeExpression(CompiledStruct @struct, Uri originalFile, ImmutableDictionary<string, CompiledTypeExpression> typeArguments, Location location) : base(location)
    {
        Struct = @struct;
        TypeArguments = typeArguments;
        File = originalFile;
    }

    public CompiledTypeExpression ReplaceType(CompiledTypeExpression type, out PossibleDiagnostic? error)
    {
        error = null;

        if (!type.Is(out CompiledGenericTypeExpression? genericType))
        { return type; }

        if (!TypeArguments.TryGetValue(genericType.Identifier, out CompiledTypeExpression? result))
        {
            error = new PossibleDiagnostic($"Type argument \"{genericType.Identifier}\" not found");
            return type;
        }

        return result;
    }

    public override bool Equals(object? other) => Equals(other as CompiledStructTypeExpression);
    public override bool Equals(CompiledTypeExpression? other) => Equals(other as CompiledStructTypeExpression);
    public bool Equals(CompiledStructTypeExpression? other)
    {
        if (other is null) return false;
        if (!ReferenceEquals(Struct, other.Struct)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;

        if (other is not TypeInstanceSimple otherSimple)
        { return false; }

        if (TypeKeywords.BasicTypes.ContainsKey(otherSimple.Identifier.Content))
        { return false; }

        if (Struct.Identifier.Content == otherSimple.Identifier.Content)
        { return true; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Struct);
    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(Struct.Identifier.Content);

        if (!TypeArguments.IsEmpty)
        { result.Append($"<{string.Join(", ", TypeArguments.Values)}>"); }
        else if (Struct.Template is not null)
        { result.Append($"<{string.Join(", ", Struct.Template.Parameters)}>"); }

        return result.ToString();
    }
    public override string Stringify(int depth = 0)
    {
        StringBuilder result = new();
        result.Append(Struct.Identifier.Content);

        if (!TypeArguments.IsEmpty)
        { result.Append($"<{string.Join(", ", TypeArguments.Values.Select(v => v.Stringify(depth)))}>"); }
        else if (Struct.Template is not null)
        { result.Append($"<{string.Join(", ", Struct.Template.Parameters)}>"); }

        return result.ToString();
    }

    public static CompiledStructTypeExpression CreateAnonymous(StructType type, ILocated location)
    {
        return new(
            type.Struct,
            type.File,
            type.TypeArguments?.Select(v => new KeyValuePair<string, CompiledTypeExpression>(v.Key, CreateAnonymous(v.Value, location))).ToImmutableDictionary() ?? ImmutableDictionary<string, CompiledTypeExpression>.Empty,
            location.Location
        );
    }
}
