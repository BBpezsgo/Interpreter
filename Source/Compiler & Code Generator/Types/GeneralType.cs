﻿namespace LanguageCore.Compiler;

using Runtime;
using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public abstract class GeneralType :
    IEquatable<GeneralType>,
    IEquatable<TypeInstance>,
    IEquatable<BasicType>,
    IEquatable<RuntimeType>
{
    public abstract int Size { get; }
    public abstract int SizeBytes { get; }
    /// <exception cref="InvalidOperationException"/>
    public abstract BitWidth BitWidth { get; }

    public virtual GeneralType FinalValue => this;
    public bool CanBeBuiltin => FinalValue is BuiltinType or PointerType;

    public static GeneralType From(GeneralType other) => other switch
    {
        BuiltinType v => new BuiltinType(v),
        StructType v => new StructType(v),
        GenericType v => new GenericType(v),
        FunctionType v => new FunctionType(v),
        ArrayType v => new ArrayType(v),
        PointerType v => new PointerType(v),
        AliasType v => new AliasType(v),
        _ => throw new NotImplementedException()
    };

    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="CompilerException"/>
    public static GeneralType From(
        TypeInstance type,
        FindType typeFinder,
        ComputeValue? constComputer = null,
        Uri? uri = null) => type switch
        {
            TypeInstanceSimple simpleType => GeneralType.From(simpleType, typeFinder, constComputer, uri),
            TypeInstanceFunction functionType => GeneralType.From(functionType, typeFinder, constComputer, uri),
            TypeInstanceStackArray stackArrayType => GeneralType.From(stackArrayType, typeFinder, constComputer, uri),
            TypeInstancePointer pointerType => GeneralType.From(pointerType, typeFinder, constComputer, uri),
            _ => throw new UnreachableException(),
        };

    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    public static ArrayType From(
        TypeInstanceStackArray type,
        FindType typeFinder,
        ComputeValue? constComputer = null,
        Uri? uri = null)
    {
        CompiledValue stackArraySize;

        if (type.StackArraySize is null)
        { throw new NotImplementedException(); }

        if (constComputer is not null)
        {
            if (!constComputer.Invoke(type.StackArraySize, out stackArraySize))
            { throw new CompilerException("Failed to compute array size value", type.StackArraySize, uri); }
        }
        else
        {
            if (!CodeGenerator.TryComputeSimple(type.StackArraySize, out stackArraySize))
            { throw new CompilerException("Can't compute array size value", type.StackArraySize, uri); }
        }

        GeneralType? of = GeneralType.From(type.StackArrayOf, typeFinder, constComputer, uri);

        int size = (int)stackArraySize;

        ArrayType result = new(of, size);
        type.SetAnalyzedType(result);

        return result;
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    public static FunctionType From(
        TypeInstanceFunction type,
        FindType typeFinder,
        ComputeValue? constComputer = null,
        Uri? uri = null)
    {
        GeneralType returnType = GeneralType.From(type.FunctionReturnType, typeFinder, constComputer, uri);
        IEnumerable<GeneralType> parameters = GeneralType.FromArray(type.FunctionParameterTypes, typeFinder, constComputer, uri);

        FunctionType result = new(returnType, parameters);
        type.SetAnalyzedType(result);

        return result;
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    public static PointerType From(
        TypeInstancePointer type,
        FindType typeFinder,
        ComputeValue? constComputer = null,
        Uri? uri = null)
    {
        GeneralType to = GeneralType.From(type.To, typeFinder, constComputer, uri);

        PointerType result = new(to);
        type.SetAnalyzedType(result);

        return result;
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="ArgumentNullException"/>
    public static GeneralType From(
        TypeInstanceSimple type,
        FindType typeFinder,
        ComputeValue? constComputer = null,
        Uri? uri = null)
    {
        GeneralType? result;

        if (TypeKeywords.BasicTypes.TryGetValue(type.Identifier.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            type.SetAnalyzedType(result);
            return result;
        }

        if (!typeFinder.Invoke(type.Identifier, type.File, out result))
        { throw new CompilerException($"Can't parse \"{type}\" to {nameof(GeneralType)}", type, uri); }

        if (result.Is(out StructType? resultStructType) &&
            resultStructType.Struct.Template is not null)
        {
            if (type.TypeArguments.HasValue)
            {
                IEnumerable<GeneralType> typeParameters = GeneralType.FromArray(type.TypeArguments.Value, typeFinder, constComputer, uri);
                result = new StructType(resultStructType.Struct, type.File, typeParameters.ToImmutableList());
            }
            else
            {
                result = new StructType(resultStructType.Struct, type.File);
            }
        }
        else
        {
            if (type.TypeArguments.HasValue)
            { throw new InternalException($"Asd"); }
        }

        type.SetAnalyzedType(result);
        return result;
    }

    public static IEnumerable<GeneralType> FromArray(
        IEnumerable<TypeInstance>? types,
        FindType typeFinder,
        ComputeValue? constComputer = null,
        Uri? uri = null)
    {
        if (types is null) yield break;

        foreach (TypeInstance item in types)
        { yield return GeneralType.From(item, typeFinder, constComputer, uri); }
    }

    public static IEnumerable<GeneralType> FromArray(
        IEnumerable<IHaveType>? types,
        FindType typeFinder,
        ComputeValue? constComputer = null,
        Uri? uri = null)
        => GeneralType.FromArray(types?.Select(v => v.Type), typeFinder, constComputer, uri);

    [Obsolete]
    public static bool operator ==(GeneralType? a, GeneralType? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        return a.Equals(b);
    }
    [Obsolete]
    public static bool operator !=(GeneralType? a, GeneralType? b) => !(a == b);

    [Obsolete]
    public static bool operator ==([NotNullWhen(true)] GeneralType? a, RuntimeType b) => a is not null && a.Equals(b);
    [Obsolete]
    public static bool operator !=(GeneralType? a, RuntimeType b) => !(a == b);

    [Obsolete]
    public static bool operator ==([NotNullWhen(true)] GeneralType? a, BasicType b) => a is not null && a.Equals(b);
    [Obsolete]
    public static bool operator !=(GeneralType? a, BasicType b) => !(a == b);

    public bool SameAs([NotNullWhen(true)] GeneralType? other)
    {
        if (other is null) return false;
        return FinalValue.Equals(other.FinalValue);
    }
    public bool SameAs(RuntimeType other) => FinalValue.Equals(other);
    public bool SameAs(BasicType other) => FinalValue.Equals(other);

    public bool Is<T>() where T : GeneralType => FinalValue is T;
    public bool Is<T>([NotNullWhen(true)] out T? value) where T : GeneralType
    {
        if (FinalValue is T _value)
        {
            value = _value;
            return true;
        }
        else
        {
            value = null;
            return false;
        }
    }

    public abstract override bool Equals(object? obj);
    public abstract bool Equals(GeneralType? other);
    public abstract bool Equals(TypeInstance? other);
    public abstract override int GetHashCode();
    public abstract override string ToString();
    public bool Equals(BasicType other) => FinalValue is BuiltinType builtinType && builtinType.Type == other;
    public bool Equals(RuntimeType other) => FinalValue switch
    {
        BuiltinType builtinType => builtinType.RuntimeType == other,
        PointerType => other == RuntimeType.Integer,
        _ => false,
    };

    /// <exception cref="NotImplementedException"/>
    public static bool TryGetTypeParameters(IEnumerable<GeneralType> definedParameters, IEnumerable<GeneralType> passedParameters, Dictionary<string, GeneralType> typeParameters)
        => GeneralType.TryGetTypeParameters(definedParameters.ToImmutableArray(), passedParameters.ToImmutableArray(), typeParameters);

    /// <exception cref="NotImplementedException"/>
    public static bool TryGetTypeParameters(ImmutableArray<GeneralType> definedParameters, ImmutableArray<GeneralType> passedParameters, Dictionary<string, GeneralType> typeParameters)
    {
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
        if (passed.Is(out GenericType? passedGenericType))
        {
            if (typeParameters.ContainsKey(passedGenericType.Identifier))
            { return false; }
            throw new NotImplementedException($"This should be non-generic");
        }

        if (defined.Is(out GenericType? definedGenericType))
        {
            if (typeParameters.TryGetValue(definedGenericType.Identifier, out GeneralType? addedTypeParameter))
            {
                if (!addedTypeParameter.SameAs(passed)) return false;
            }
            else
            {
                typeParameters.Add(definedGenericType.Identifier, passed);
            }

            return true;
        }

        if (defined.Is(out PointerType? definedPointerType) && passed.Is(out PointerType? passedPointerType))
        {
            return TryGetTypeParameters(definedPointerType.To, passedPointerType.To, typeParameters);
        }

        if (defined.Is(out StructType? definedStructType) && passed.Is(out StructType? passedStructType))
        {
            if (definedStructType.Struct.Identifier.Content != passedStructType.Struct.Identifier.Content) return false;
            if (definedStructType.Struct.Template is not null && passedStructType.Struct.Template is not null)
            {
                if (definedStructType.Struct.Template.Parameters.Length != passedStructType.TypeArguments.Count)
                { throw new NotImplementedException(); }

                for (int i = 0; i < definedStructType.Struct.Template.Parameters.Length; i++)
                {
                    string typeParamName = definedStructType.Struct.Template.Parameters[i].Content;
                    GeneralType typeParamValue = passedStructType.TypeArguments[typeParamName];

                    if (typeParameters.TryGetValue(typeParamName, out GeneralType? addedTypeParameter))
                    { if (!addedTypeParameter.SameAs(typeParamValue)) return false; }
                    else
                    { typeParameters.Add(typeParamName, typeParamValue); }
                }

                return true;
            }
        }

        if (!defined.SameAs(passed)) return false;

        return true;
    }

    public bool AllGenericsDefined()
    {
        switch (FinalValue)
        {
            case GenericType: return false;
            case BuiltinType: return true;
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
                if (structType.Struct.Template is null) return true;
                return structType.TypeArguments is not null;
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
                if (structType.Struct.Template is not null)
                {
                    GeneralType[] structTypeParameterValues = new GeneralType[structType.Struct.Template.Parameters.Length];

                    foreach (KeyValuePair<string, GeneralType> item in typeArguments)
                    {
                        if (structType.Struct.TryGetTypeArgumentIndex(item.Key, out int i))
                        { structTypeParameterValues[i] = item.Value; }
                    }

                    for (int i = 0; i < structTypeParameterValues.Length; i++)
                    {
                        if (structTypeParameterValues[i] is null or GenericType)
                        { return null; }
                    }

                    return new StructType(structType.Struct, structType.File, structTypeParameterValues);
                }

                return null;
            }

            case BuiltinType:
                return null;

            case AliasType: // TODO
                return null;

            default:
                throw new NotImplementedException();
        }
    }

    public static IEnumerable<GeneralType> InsertTypeParameters(IEnumerable<GeneralType> types, IReadOnlyDictionary<string, GeneralType> typeArguments)
    {
        foreach (GeneralType type in types)
        {
            yield return GeneralType.InsertTypeParameters(type, typeArguments) ?? type;
        }
    }
}
