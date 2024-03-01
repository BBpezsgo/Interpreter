namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;

public enum BasicType
{
    Void,
    Byte,
    Integer,
    Float,
    Char,
}

public delegate bool ComputeValue(StatementWithValue value, out DataItem computedValue);
public delegate bool FindType(Token token, [NotNullWhen(true)] out GeneralType? computedValue);

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public abstract class GeneralType :
    IEquatable<GeneralType>,
    IEquatable<TypeInstance>,
    IEquatable<BasicType>,
    IEquatable<RuntimeType>
{
    public bool CanBeBuiltin => this is BuiltinType or EnumType or PointerType;

    public abstract int Size { get; }

    public static GeneralType From(GeneralType other) => other switch
    {
        BuiltinType v => new BuiltinType(v),
        EnumType v => new EnumType(v),
        StructType v => new StructType(v),
        GenericType v => new GenericType(v),
        FunctionType v => new FunctionType(v),
        ArrayType v => new ArrayType(v),
        PointerType v => new PointerType(v),
        _ => throw new NotImplementedException()
    };

    /// <exception cref="InternalException"/>
    public static GeneralType From(Token type, FindType? typeFinder)
    {
        if (LanguageConstants.BuiltinTypeMap3.TryGetValue(type.Content, out BasicType builtinType))
        { return new BuiltinType(builtinType); }

        if (typeFinder == null ||
            !typeFinder.Invoke(type, out GeneralType? result))
        { throw new InternalException($"Can't parse \"{type}\" to {nameof(GeneralType)}"); }

        return result;
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    public static GeneralType From(TypeInstance type, FindType? typeFinder, ComputeValue? constComputer = null)
    {
        GeneralType result = type switch
        {
            TypeInstanceSimple simpleType => From(simpleType, typeFinder),
            TypeInstanceFunction functionType => new FunctionType(functionType, typeFinder, constComputer),
            TypeInstanceStackArray stackArrayType => new ArrayType(stackArrayType, typeFinder, constComputer),
            TypeInstancePointer pointerType => new PointerType(pointerType, typeFinder, constComputer),
            _ => throw new UnreachableException()
        };
        type.SetAnalyzedType(result);
        return result;
    }
    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    public static GeneralType From(IHaveType type, FindType? typeFinder, ComputeValue? constComputer = null)
        => GeneralType.From(type.Type, typeFinder, constComputer);
    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    public static GeneralType From(TypeInstanceSimple type, FindType? typeFinder)
    {
        if (LanguageConstants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out BasicType builtinType))
        { return new BuiltinType(builtinType); }

        ArgumentNullException.ThrowIfNull(typeFinder);

        if (!typeFinder.Invoke(type.Identifier, out GeneralType? result))
        { throw new InternalException($"Can't parse \"{type}\" to {nameof(GeneralType)}"); }

        if (result is StructType resultStructType &&
            resultStructType.Struct.TemplateInfo is not null)
        {
            if (type.GenericTypes.HasValue)
            {
                IEnumerable<GeneralType> typeParameters = GeneralType.FromArray(type.GenericTypes.Value, typeFinder);
                return new StructType(resultStructType.Struct, typeParameters);
            }
            else
            {
                return new StructType(resultStructType.Struct);
            }
        }
        else
        {
            if (type.GenericTypes.HasValue)
            { throw new CompilerException($"Asd", new Position(type.GenericTypes.Value)); }
            return result;
        }
    }

    public static IEnumerable<GeneralType> FromArray(IEnumerable<TypeInstance>? types, FindType? typeFinder, ComputeValue? constComputer = null)
    {
        if (types is null) yield break;

        foreach (TypeInstance type in types)
        { yield return GeneralType.From(type, typeFinder, constComputer); }
    }

    public static IEnumerable<GeneralType> FromArray(IEnumerable<IHaveType>? types, FindType? typeFinder, ComputeValue? constComputer = null)
    {
        if (types is null) yield break;

        foreach (IHaveType type in types)
        { yield return GeneralType.From(type, typeFinder, constComputer); }
    }

    public static bool operator ==(GeneralType? a, GeneralType? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        return a.Equals(b);
    }
    public static bool operator !=(GeneralType? a, GeneralType? b) => !(a == b);

    public static bool operator ==(GeneralType? a, RuntimeType b) => a is not null && a.Equals(b);
    public static bool operator !=(GeneralType? a, RuntimeType b) => !(a == b);

    public static bool operator ==(GeneralType? a, BasicType b) => a is not null && a.Equals(b);
    public static bool operator !=(GeneralType? a, BasicType b) => !(a == b);

    public abstract override bool Equals(object? obj);
    public abstract bool Equals(GeneralType? other);
    public abstract bool Equals(TypeInstance? other);
    public abstract override int GetHashCode();
    public abstract override string ToString();
    public bool Equals(BasicType other) => this is BuiltinType builtinType && builtinType.Type == other;
    public bool Equals(RuntimeType other)
    {
        if (this is EnumType enumType)
        {
            for (int i = 0; i < enumType.Enum.Members.Length; i++)
            {
                if (enumType.Enum.Members[i].ComputedValue.Type == other)
                { return true; }
            }
        }

        if (this is BuiltinType builtinType)
        {
            return builtinType.RuntimeType == other;
        }

        return false;
    }

    public static bool AreEquals(IEnumerable<GeneralType?>? itemsA, IEnumerable<TypeInstance?>? itemsB)
    {
        if (itemsA is null && itemsB is null) return true;
        if (itemsA is null || itemsB is null) return false;

        IEnumerator<GeneralType?> enumA = itemsA.GetEnumerator();
        IEnumerator<TypeInstance?> enumB = itemsB.GetEnumerator();

        while (true)
        {
            GeneralType? a = enumA.Current;
            TypeInstance? b = enumB.Current;

            if (enumA.MoveNext() != enumB.MoveNext()) return false;

            if (a is null && b is null) continue;
            if (a is null || b is null) return false;

            if (!a.Equals(b)) return false;
        }
    }

    public static bool AreEquals(IEnumerable<GeneralType?>? itemsA, IEnumerable<GeneralType?>? itemsB)
        => AreEquals(itemsA?.ToArray(), itemsB?.ToArray());

    public static bool AreEquals(GeneralType?[]? itemsA, GeneralType?[]? itemsB)
    {
        if (itemsA is null && itemsB is null) return true;
        if (itemsA is null || itemsB is null) return false;

        if (itemsA.Length != itemsB.Length) return false;

        for (int i = 0; i < itemsA.Length; i++)
        {
            GeneralType? a = itemsA[i];
            GeneralType? b = itemsB[i];

            if (a is null && b is null) continue;
            if (a is null || b is null) return false;

            if (!a.Equals(b)) return false;
        }

        return true;
    }

    /// <exception cref="NotImplementedException"/>
    public static bool TryGetTypeParameters(IEnumerable<GeneralType>? definedParameters, IEnumerable<GeneralType>? passedParameters, Dictionary<string, GeneralType> typeParameters)
        => GeneralType.TryGetTypeParameters(definedParameters?.ToArray(), passedParameters?.ToArray(), typeParameters);

    /// <exception cref="NotImplementedException"/>
    public static bool TryGetTypeParameters(GeneralType[]? definedParameters, GeneralType[]? passedParameters, Dictionary<string, GeneralType> typeParameters)
    {
        if (definedParameters is null || passedParameters is null) return false;
        if (definedParameters.Length != passedParameters.Length) return false;

        for (int i = 0; i < definedParameters.Length; i++)
        {
            if (!TryGetTypeParameters(definedParameters[i], passedParameters[i], typeParameters))
            { return false; }
        }

        return true;
    }

    public static bool TryGetTypeParameters(GeneralType defined, GeneralType passed, Dictionary<string, GeneralType> typeParameters)
    {
        if (passed is GenericType passedGenericType)
        {
            if (typeParameters.ContainsKey(passedGenericType.Identifier))
            { return false; }
            throw new NotImplementedException($"This should be non-generic");
        }

        if (defined is GenericType definedGenericType)
        {
            if (typeParameters.TryGetValue(definedGenericType.Identifier, out GeneralType? addedTypeParameter))
            {
                if (addedTypeParameter != passed) return false;
            }
            else
            {
                typeParameters.Add(definedGenericType.Identifier, passed);
            }

            return true;
        }

        if (defined is PointerType definedPointerType && passed is PointerType passedPointerType)
        {
            return TryGetTypeParameters(definedPointerType.To, passedPointerType.To, typeParameters);
        }

        if (defined is StructType definedStructType && passed is StructType passedStructType)
        {
            if (definedStructType.Struct.Identifier.Content != passedStructType.Struct.Identifier.Content) return false;
            if (definedStructType.Struct.TemplateInfo is not null && passedStructType.Struct.TemplateInfo is not null)
            {
                if (definedStructType.Struct.TemplateInfo.TypeParameters.Length != passedStructType.TypeParameters.Length)
                { throw new NotImplementedException(); }
                for (int j = 0; j < definedStructType.Struct.TemplateInfo.TypeParameters.Length; j++)
                {
                    string typeParamName = definedStructType.Struct.TemplateInfo.TypeParameters[j].Content;
                    GeneralType typeParamValue = passedStructType.TypeParameters[j];

                    if (typeParameters.TryGetValue(typeParamName, out GeneralType? addedTypeParameter))
                    { if (addedTypeParameter != typeParamValue) return false; }
                    else
                    { typeParameters.Add(typeParamName, typeParamValue); }
                }

                return true;
            }
        }

        if (!defined.Equals(passed)) return false;

        return true;
    }

    public bool AllGenericsDefined()
    {
        switch (this)
        {
            case GenericType: return false;
            case BuiltinType: return true;
            case EnumType: return true;
            case PointerType pointerType: return pointerType.To.AllGenericsDefined();
            case ArrayType arrayType: return arrayType.Of.AllGenericsDefined();

            case FunctionType functionType:
            {
                for (int i = 0; i < functionType.Parameters.Length; i++)
                {
                    if (!functionType.Parameters[i].AllGenericsDefined())
                    { return false; }
                }
                return functionType.ReturnType.AllGenericsDefined();
            }

            case StructType structType:
            {
                if (structType.Struct.TemplateInfo == null) return true;
                return structType.TypeParameters.Length > 0;
            }

            default: throw new NotImplementedException();
        }
    }

    public static GeneralType? InsertTypeParameters(GeneralType type, IReadOnlyDictionary<string, GeneralType>? typeArguments)
    {
        if (typeArguments is null) return null;

        switch (type)
        {
            case GenericType genericType:
            {
                if (!typeArguments.TryGetValue(genericType.Identifier, out GeneralType? passedTypeArgument))
                { throw new InternalException(); }
                return passedTypeArgument;
            }

            case PointerType pointerType:
            {
                GeneralType? pointerTo = InsertTypeParameters(pointerType.To, typeArguments);
                if (pointerTo is null) return null;
                return new PointerType(pointerTo);
            }

            case ArrayType arrayType:
            {
                GeneralType? stackArrayOf = InsertTypeParameters(arrayType.Of, typeArguments);
                if (stackArrayOf is null) return null;
                return new ArrayType(stackArrayOf, arrayType.Length);
            }

            case StructType structType:
            {
                if (structType.Struct.TemplateInfo != null)
                {
                    GeneralType[] structTypeParameterValues = new GeneralType[structType.Struct.TemplateInfo.TypeParameters.Length];

                    foreach (KeyValuePair<string, GeneralType> item in typeArguments)
                    {
                        if (structType.Struct.TryGetTypeArgumentIndex(item.Key, out int j))
                        { structTypeParameterValues[j] = item.Value; }
                    }

                    for (int j = 0; j < structTypeParameterValues.Length; j++)
                    {
                        if (structTypeParameterValues[j] is null ||
                            structTypeParameterValues[j] is GenericType)
                        { return null; }
                    }

                    return new StructType(structType.Struct, structTypeParameterValues);
                }

                break;
            }
        }

        return null;
    }

    public static IEnumerable<GeneralType> InsertTypeParameters(IEnumerable<GeneralType> types, IReadOnlyDictionary<string, GeneralType> typeArguments)
    {
        foreach (GeneralType type in types)
        {
            yield return GeneralType.InsertTypeParameters(type, typeArguments) ?? type;
        }
    }
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class BuiltinType : GeneralType,
    IEquatable<BuiltinType>
{
    public BasicType Type { get; }

    /// <exception cref="InternalException"/>
    /// <exception cref="NotImplementedException"/>
    public RuntimeType RuntimeType => Type switch
    {
        BasicType.Byte => RuntimeType.Byte,
        BasicType.Integer => RuntimeType.Integer,
        BasicType.Float => RuntimeType.Single,
        BasicType.Char => RuntimeType.Char,

        _ => throw new NotImplementedException($"Type conversion for {Type} is not implemented"),
    };

    public override int Size => 1;

    public BuiltinType(BuiltinType other)
    {
        Type = other.Type;
    }

    public BuiltinType(BasicType type)
    {
        Type = type;
    }

    /// <exception cref="NotImplementedException"/>
    public BuiltinType(RuntimeType type)
    {
        Type = type switch
        {
            RuntimeType.Byte => BasicType.Byte,
            RuntimeType.Integer => BasicType.Integer,
            RuntimeType.Single => BasicType.Float,
            RuntimeType.Char => BasicType.Char,
            RuntimeType.Null => throw new NotImplementedException(),
            _ => throw new UnreachableException(),
        };
    }

    public override bool Equals(object? other) => Equals(other as BuiltinType);
    public override bool Equals(GeneralType? other) => Equals(other as BuiltinType);
    public bool Equals(BuiltinType? other)
    {
        if (other is null) return false;
        if (Type != other.Type) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;

        if (other is not TypeInstanceSimple otherSimple)
        { return false; }

        if (LanguageConstants.BuiltinTypeMap3.TryGetValue(otherSimple.Identifier.Content, out BasicType type))
        { return Type == type; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Type);
    public override string ToString() => Type switch
    {
        BasicType.Void => "void",
        BasicType.Byte => "byte",
        BasicType.Integer => "int",
        BasicType.Float => "float",
        BasicType.Char => "char",
        _ => throw new UnreachableException(),
    };
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class StructType : GeneralType,
    IEquatable<StructType>
{
    public CompiledStruct Struct { get; }
    public ImmutableArray<GeneralType> TypeParameters { get; protected init; }
    public IReadOnlyDictionary<string, GeneralType>? TypeParametersMap => Struct.TemplateInfo?.ToDictionary(TypeParameters);

    public override int Size
    {
        get
        {
            Dictionary<string, GeneralType> saved = new(Struct.CurrentTypeArguments);
            Struct.SetTypeArguments(TypeParameters);
            int size = Struct.Size;
            Struct.SetTypeArguments(saved);
            return size;
        }
    }

    public StructType(StructType other)
    {
        Struct = other.Struct;
        TypeParameters = other.TypeParameters;
    }

    public StructType(CompiledStruct @struct)
    {
        Struct = @struct;
        if (@struct.TemplateInfo is not null)
        { TypeParameters = @struct.TemplateInfo.TypeParameterNames.Select(v => new GenericType(v)).Cast<GeneralType>().ToImmutableArray(); }
        else
        { TypeParameters = ImmutableArray.Create<GeneralType>(); }
    }

    public StructType(CompiledStruct @struct, IEnumerable<GeneralType> typeParameters)
    {
        Struct = @struct;
        TypeParameters = typeParameters.ToImmutableArray();
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

        if (LanguageConstants.BuiltinTypeMap3.ContainsKey(otherSimple.Identifier.Content))
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

        if (TypeParameters.Length > 0)
        { result.Append($"<{string.Join(", ", TypeParameters)}>"); }
        else if (Struct.TemplateInfo is not null)
        { result.Append($"<{string.Join(", ", Struct.TemplateInfo.TypeParameters)}>"); }

        return result.ToString();
    }
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class GenericType : GeneralType,
    IEquatable<GenericType>
{
    public string Identifier { get; }

    public override int Size
    {
        [DoesNotReturn]
        get => throw new InternalException($"Can not get the size of a generic type");
    }

    public GenericType(GenericType other)
    {
        Identifier = other.Identifier;
    }

    public GenericType(string identifier)
    {
        Identifier = identifier;
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

        if (LanguageConstants.BuiltinTypeMap3.ContainsKey(otherSimple.Identifier.Content))
        { return false; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Identifier);
    public override string ToString() => Identifier;
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class EnumType : GeneralType,
    IEquatable<EnumType>
{
    public CompiledEnum Enum { get; }

    public override int Size => 1;

    public EnumType(EnumType other)
    {
        Enum = other.Enum;
    }

    public EnumType(CompiledEnum @enum)
    {
        Enum = @enum;
    }

    public override bool Equals(object? other) => Equals(other as EnumType);
    public override bool Equals(GeneralType? other) => Equals(other as EnumType);
    public bool Equals(EnumType? other)
    {
        if (other is null) return false;
        if (!object.ReferenceEquals(Enum, other.Enum)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;

        if (other is not TypeInstanceSimple otherSimple)
        { return false; }

        if (LanguageConstants.BuiltinTypeMap3.ContainsKey(otherSimple.Identifier.Content))
        { return false; }

        if (Enum.Identifier.Content == otherSimple.Identifier.Content)
        { return true; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Enum);
    public override string ToString() => Enum.Identifier.Content;
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class PointerType : GeneralType,
    IEquatable<PointerType>
{
    public GeneralType To { get; }

    public override int Size => 1;

    public PointerType(PointerType other)
    {
        To = other.To;
    }

    public PointerType(GeneralType to)
    {
        To = to;
    }

    public PointerType(TypeInstancePointer type, FindType? typeFinder, ComputeValue? constComputer = null)
    {
        To = GeneralType.From(type.To, typeFinder, constComputer);
    }

    public override bool Equals(object? other) => Equals(other as PointerType);
    public override bool Equals(GeneralType? other) => Equals(other as PointerType);
    public bool Equals(PointerType? other)
    {
        if (other is null) return false;
        if (!To.Equals(other.To)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstancePointer otherPointer) return false;
        if (!To.Equals(otherPointer.To)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(To);
    public override string ToString() => $"{To}*";
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class ArrayType : GeneralType,
    IEquatable<ArrayType>
{
    public GeneralType Of { get; }
    public int Length { get; }

    public override int Size => Length * Of.Size;

    public ArrayType(ArrayType other)
    {
        Of = other.Of;
        Length = other.Length;
    }

    public ArrayType(GeneralType of, int size)
    {
        Of = of;
        Length = size;
    }

    /// <exception cref="CompilerException"/>
    public ArrayType(TypeInstanceStackArray type, FindType? typeFinder, ComputeValue? constComputer = null)
    {
        if ((
                constComputer is null ||
                constComputer.Invoke(type.StackArraySize!, out DataItem stackArraySize)
           ) &&
           !CodeGenerator.TryComputeSimple(type.StackArraySize, out stackArraySize))
        { throw new CompilerException($"Failed to compute array size value", type.StackArraySize); }

        Of = GeneralType.From(type.StackArrayOf, typeFinder, constComputer);
        Length = (int)stackArraySize;
    }

    public override bool Equals(object? other) => Equals(other as ArrayType);
    public override bool Equals(GeneralType? other) => Equals(other as ArrayType);
    public bool Equals(ArrayType? other)
    {
        if (other is null) return false;
        if (!Of.Equals(other.Of)) return false;
        if (!Length.Equals(other.Length)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstanceStackArray otherStackArray) return false;
        if (!Of.Equals(otherStackArray.StackArrayOf)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(Of, Length);
    public override string ToString() => $"{Of}[{Length}]";
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class FunctionType : GeneralType,
    IEquatable<FunctionType>,
    IEquatable<CompiledFunction>
{
    public GeneralType ReturnType { get; }
    public ImmutableArray<GeneralType> Parameters { get; }
    public bool ReturnSomething => ReturnType != BasicType.Void;

    public override int Size => 1;

    public FunctionType(CompiledFunction function)
    {
        ReturnType = function.Type;
        Parameters = function.ParameterTypes;
    }

    public FunctionType(FunctionType other)
    {
        ReturnType = other.ReturnType;
        Parameters = other.Parameters;
    }

    public FunctionType(GeneralType returnType, IEnumerable<GeneralType> parameters)
    {
        ReturnType = returnType;
        Parameters = parameters.ToImmutableArray();
    }

    public FunctionType(TypeInstanceFunction type, FindType? typeFinder = null, ComputeValue? constComputer = null)
    {
        ReturnType = GeneralType.From(type.FunctionReturnType, typeFinder, constComputer);
        Parameters = GeneralType.FromArray(type.FunctionParameterTypes, typeFinder).ToImmutableArray();
    }

    public override bool Equals(object? other) => Equals(other as FunctionType);
    public override bool Equals(GeneralType? other) => Equals(other as FunctionType);
    public bool Equals(FunctionType? other)
    {
        if (other is null) return false;
        if (!other.ReturnType.Equals(ReturnType)) return false;
        if (!GeneralType.AreEquals(Parameters, other.Parameters)) return false;
        return true;
    }
    public bool Equals(CompiledFunction? other)
    {
        if (other is null) return false;
        if (!other.Type.Equals(ReturnType)) return false;
        if (!GeneralType.AreEquals(Parameters, other.ParameterTypes)) return false;
        return true;
    }
    public override bool Equals(TypeInstance? other)
    {
        if (other is null) return false;
        if (other is not TypeInstanceFunction otherFunction) return false;
        if (!ReturnType.Equals(otherFunction.FunctionReturnType)) return false;
        if (!GeneralType.AreEquals(Parameters, otherFunction.FunctionParameterTypes)) return false;
        return true;
    }
    public override int GetHashCode() => HashCode.Combine(ReturnType, Parameters, ReturnSomething);

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(ReturnType.ToString());
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(Parameters[i].ToString());
        }
        result.Append(')');

        return result.ToString();
    }

    public static bool operator ==(FunctionType? left, FunctionType? right) => EqualityComparer<FunctionType>.Default.Equals(left, right);
    public static bool operator !=(FunctionType? left, FunctionType? right) => !(left == right);
}
