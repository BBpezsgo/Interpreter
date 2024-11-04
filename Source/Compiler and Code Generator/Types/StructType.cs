using LanguageCore.Runtime;
using LanguageCore.Parser;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class StructType : GeneralType,
    IEquatable<StructType>,
    IReferenceableTo<CompiledStruct>
{
    public CompiledStruct Struct { get; }
    public ImmutableDictionary<string, GeneralType> TypeArguments { get; }
    public Uri File { get; }

    public ImmutableDictionary<CompiledField, int> GetFields(IRuntimeInfoProvider runtime)
    {
        Dictionary<CompiledField, int> result = new(Struct.Fields.Length);

        int offset = 0;
        foreach (CompiledField field in Struct.Fields)
        {
            result.Add(field, offset);
            GeneralType fieldType = field.Type;
            fieldType = ReplaceType(fieldType, out PossibleDiagnostic? error);
            error?.Throw();
            offset += fieldType.GetSize(runtime);
        }

        return result.ToImmutableDictionary();
    }

    CompiledStruct? IReferenceableTo<CompiledStruct>.Reference
    {
        get => Struct;
        set => throw new InvalidOperationException();
    }

    StructType(CompiledStruct @struct, Uri file, IEnumerable<KeyValuePair<string, GeneralType>> typeArguments)
    {
        Struct = @struct;
        File = file;
        TypeArguments = typeArguments.ToImmutableDictionary();
    }

    public StructType(StructType other)
    {
        Struct = other.Struct;
        TypeArguments = other.TypeArguments;
        File = other.File;
    }

    public StructType(CompiledStruct @struct, Uri originalFile)
    {
        Struct = @struct;
        if (@struct.Template is not null)
        {
            TypeArguments = @struct.Template.Parameters
                .Select(v => new KeyValuePair<string, GeneralType>(v.Content, new GenericType(v, originalFile)))
                .ToImmutableDictionary();
        }
        else
        { TypeArguments = ImmutableDictionary<string, GeneralType>.Empty; }
        File = originalFile;
    }

    public StructType(CompiledStruct @struct, Uri originalFile, IReadOnlyList<GeneralType> typeArguments)
    {
        Struct = @struct;
        if (@struct.Template is not null)
        {
            Dictionary<string, GeneralType> result = new(@struct.Template.Parameters.Length);

            if (@struct.Template.Parameters.Length != typeArguments.Count)
            { throw new InternalExceptionWithoutContext("Length of type parameters doesn't matching with length of type arguments"); }

            for (int i = 0; i < @struct.Template.Parameters.Length; i++)
            { result.Add(@struct.Template.Parameters[i].Content, typeArguments[i]); }

            TypeArguments = result.ToImmutableDictionary();
        }
        else
        { TypeArguments = ImmutableDictionary<string, GeneralType>.Empty; }
        File = originalFile;
    }

    public override int GetSize(IRuntimeInfoProvider runtime)
    {
        int size = 0;
        foreach (CompiledField field in Struct.Fields)
        {
            GeneralType fieldType = field.Type;
            fieldType = ReplaceType(fieldType, out PossibleDiagnostic? error);
            error?.Throw();
            size += fieldType.GetSize(runtime);
        }
        return size;
    }

    public override BitWidth GetBitWidth(IRuntimeInfoProvider runtime)
        => throw new InvalidOperationException();

    public bool GetField(string name, IRuntimeInfoProvider runtime, [NotNullWhen(true)] out CompiledField? field, [NotNullWhen(true)] out int offset)
    {
        offset = 0;
        field = null;

        foreach (CompiledField _field in Struct.Fields)
        {
            if (_field.Identifier.Content == name)
            {
                field = _field;
                return true;
            }

            GeneralType fieldType = _field.Type;
            fieldType = ReplaceType(fieldType, out PossibleDiagnostic? error);
            error?.Throw();

            offset += fieldType.GetSize(runtime);
        }

        return false;
    }

    public bool GetField(string name, [NotNullWhen(true)] out CompiledField? field)
    {
        foreach (CompiledField _field in Struct.Fields)
        {
            if (_field.Identifier.Content == name)
            {
                field = _field;
                return true;
            }
        }

        field = null;
        return false;
    }

    public GeneralType ReplaceType(GeneralType type, out PossibleDiagnostic? error)
    {
        error = null;

        if (!type.Is(out GenericType? genericType))
        { return type; }

        if (!TypeArguments.TryGetValue(genericType.Identifier, out GeneralType? result))
        {
            error = new PossibleDiagnostic($"Type argument \"{genericType.Identifier}\" not found");
            return type;
        }

        return result;
    }

    public override bool Equals(object? other) => Equals(other as StructType);
    public override bool Equals(GeneralType? other) => Equals(other as StructType);
    public bool Equals(StructType? other)
    {
        if (other is null) return false;
        if (!object.ReferenceEquals(Struct, other.Struct)) return false;
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
}
