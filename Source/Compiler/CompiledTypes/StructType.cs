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

    public bool GetFields(IRuntimeInfoProvider runtime, [NotNullWhen(true)] out ImmutableDictionary<CompiledField, int>? fields, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        Dictionary<CompiledField, int> result = new(Struct.Fields.Length);
        fields = default;

        int offset = 0;
        foreach (CompiledField field in Struct.Fields)
        {
            result.Add(field, offset);
            GeneralType fieldType = field.Type;
            fieldType = ReplaceType(fieldType, out error);
            if (error is not null) return false;
            if (!fieldType.GetSize(runtime, out int fieldSize, out error)) return false;
            offset += fieldSize;
        }

        fields = result.ToImmutableDictionary();
        error = null;
        return true;
    }

    CompiledStruct? IReferenceableTo<CompiledStruct>.Reference
    {
        get => Struct;
        set => throw new InvalidOperationException();
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

    public override bool GetSize(IRuntimeInfoProvider runtime, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = 0;
        error = default;

        foreach (CompiledField field in Struct.Fields)
        {
            GeneralType fieldType = field.Type;
            fieldType = ReplaceType(fieldType, out error);
            if (error is not null) return false;
            if (!fieldType.GetSize(runtime, out int fieldSize, out error)) return false;
            size += fieldSize;
        }
        return true;
    }

    public override bool GetBitWidth(IRuntimeInfoProvider runtime, out BitWidth bitWidth, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        bitWidth = default;
        error = new PossibleDiagnostic($"Cannot get bit width of struct type \"{this}\"");
        return false;
    }

    public bool GetField(string name, IRuntimeInfoProvider runtime, [NotNullWhen(true)] out CompiledField? field, [NotNullWhen(true)] out int offset, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        offset = default;
        field = default;
        error = default;

        foreach (CompiledField _field in Struct.Fields)
        {
            if (_field.Identifier.Content == name)
            {
                field = _field;
                return true;
            }

            GeneralType fieldType = _field.Type;
            fieldType = ReplaceType(fieldType, out error);
            if (error is not null) return false;
            if (!fieldType.GetSize(runtime, out int fieldSize, out error)) return false;

            offset += fieldSize;
        }

        error = new PossibleDiagnostic($"Field \"{name}\" not found in struct \"{Struct}\"");
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
