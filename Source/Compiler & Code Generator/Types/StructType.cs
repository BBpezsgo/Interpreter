﻿namespace LanguageCore.Compiler;

using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class StructType : GeneralType,
    IEquatable<StructType>
{
    public CompiledStruct Struct { get; }
    public ImmutableDictionary<string, GeneralType> TypeArguments { get; }

    public override int Size
    {
        get
        {
            int size = 0;
            foreach (CompiledField field in Struct.Fields)
            {
                GeneralType fieldType = field.Type;
                fieldType = ReplaceType(fieldType);
                size += fieldType.Size;
            }
            return size;
        }
    }

    public IEnumerable<KeyValuePair<CompiledField, int>> Fields
    {
        get
        {
            int offset = 0;
            foreach (CompiledField field in Struct.Fields)
            {
                yield return new KeyValuePair<CompiledField, int>(field, offset);
                GeneralType fieldType = field.Type;
                fieldType = ReplaceType(fieldType);
                offset += fieldType.Size;
            }
        }
    }

    public StructType(StructType other)
    {
        Struct = other.Struct;
        TypeArguments = other.TypeArguments;
    }

    public StructType(CompiledStruct @struct)
    {
        Struct = @struct;
        if (@struct.Template is not null)
        {
            TypeArguments = @struct.Template.Parameters
                .Select(v => new KeyValuePair<string, GeneralType>(v.Content, new GenericType(v.Content)))
                .ToImmutableDictionary();
        }
        else
        { TypeArguments = ImmutableDictionary<string, GeneralType>.Empty; }
    }

    public StructType(CompiledStruct @struct, IReadOnlyList<GeneralType> typeArguments)
    {
        Struct = @struct;
        if (@struct.Template is not null)
        {
            Dictionary<string, GeneralType> result = new(@struct.Template.Parameters.Length);

            if (@struct.Template.Parameters.Length != typeArguments.Count)
            { throw new InternalException("Length of type parameters doesn't matching with length of type arguments"); }

            for (int i = 0; i < @struct.Template.Parameters.Length; i++)
            { result.Add(@struct.Template.Parameters[i].Content, typeArguments[i]); }

            TypeArguments = result.ToImmutableDictionary();
        }
        else
        { TypeArguments = ImmutableDictionary<string, GeneralType>.Empty; }
    }

    public bool GetField(string name, [NotNullWhen(true)] out CompiledField? field, [NotNullWhen(true)] out int offset)
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
            fieldType = ReplaceType(fieldType);

            offset += fieldType.Size;
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

    GeneralType ReplaceType(GeneralType type)
    {
        if (type is not GenericType genericType)
        { return type; }

        if (!TypeArguments.TryGetValue(genericType.Identifier, out GeneralType? result))
        { throw new CompilerException($"Type argument \"{genericType.Identifier}\" not found", null, null); }

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

    public override TypeInstance ToTypeInstance() => TypeInstanceSimple.CreateAnonymous(Struct.Identifier.Content, TypeArguments?.Values.Select(v => v.ToTypeInstance()));
}
