using LanguageCore.Runtime;
using LanguageCore.Parser;

namespace LanguageCore.Compiler;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public abstract class GeneralType :
    IEquatable<GeneralType>,
    IEquatable<TypeInstance>,
    IEquatable<BasicType>,
    IEquatable<RuntimeType>
{
    public virtual GeneralType FinalValue => this;

    public int GetSize(IRuntimeInfoProvider runtime)
    {
        if (!GetSize(runtime, out int size, out PossibleDiagnostic? error))
        { error.Throw(); }
        return size;
    }

    public BitWidth GetBitWidth(IRuntimeInfoProvider runtime)
    {
        if (!GetBitWidth(runtime, out BitWidth bitWidth, out PossibleDiagnostic? error))
        { error.Throw(); }
        return bitWidth;
    }

    public int GetSize(IRuntimeInfoProvider runtime, DiagnosticsCollection diagnostics, ILocated location)
    {
        if (!GetSize(runtime, out int size, out PossibleDiagnostic? error))
        { diagnostics?.Add(error.ToError(location)); }
        return size;
    }

    public BitWidth GetBitWidth(IRuntimeInfoProvider runtime, DiagnosticsCollection diagnostics, ILocated location)
    {
        if (!GetBitWidth(runtime, out BitWidth bitWidth, out PossibleDiagnostic? error))
        { diagnostics?.Add(error.ToError(location)); }
        return bitWidth;
    }

    public abstract bool GetSize(IRuntimeInfoProvider runtime, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error);
    public abstract bool GetBitWidth(IRuntimeInfoProvider runtime, out BitWidth bitWidth, [NotNullWhen(false)] out PossibleDiagnostic? error);

    public static bool From(
        TypeInstance type,
        FindType typeFinder,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        ComputeValue? constComputer = null) => type switch
        {
            TypeInstanceSimple simpleType => From(simpleType, typeFinder, out result, out error, constComputer),
            TypeInstanceFunction functionType => From(functionType, typeFinder, out result, out error, constComputer),
            TypeInstanceStackArray stackArrayType => From(stackArrayType, typeFinder, out result, out error, constComputer),
            TypeInstancePointer pointerType => From(pointerType, typeFinder, out result, out error, constComputer),
            _ => throw new UnreachableException(),
        };

    public static bool From(
        TypeInstanceStackArray type,
        FindType typeFinder,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        ComputeValue? constComputer = null)
    {
        result = null;
        error = null;
        CompiledValue? stackArraySize = default;

        if (type.StackArraySize is not null)
        {
            if (constComputer is not null)
            {
                if (constComputer.Invoke(type.StackArraySize, out CompiledValue _stackArraySize))
                { stackArraySize = _stackArraySize; }
            }
            else
            {
                if (StatementCompiler.TryComputeSimple(type.StackArraySize, out CompiledValue _stackArraySize))
                { stackArraySize = _stackArraySize; }
            }

            if (!From(type.StackArrayOf, typeFinder, out GeneralType? of, out error, constComputer)) return false;

            result = new ArrayType(of, stackArraySize.HasValue ? new CompiledConstantValue()
            {
                Value = stackArraySize.Value,
                Location = type.StackArraySize.Location,
                SaveValue = true,
                Type = BuiltinType.I32,
            } : null);
            //type.SetAnalyzedType(result);
            return true;
        }
        else
        {
            if (!From(type.StackArrayOf, typeFinder, out GeneralType? of, out error, constComputer)) return false;
            result = new ArrayType(of, null);
            //type.SetAnalyzedType(result);
            return true;
        }
    }

    public static bool From(
        TypeInstanceFunction type,
        FindType typeFinder,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        ComputeValue? constComputer = null)
    {
        result = null;

        if (!From(type.FunctionReturnType, typeFinder, out GeneralType? returnType, out error, constComputer)) return false;
        if (!FromArray(type.FunctionParameterTypes, typeFinder, out ImmutableArray<GeneralType> parameters, out error, constComputer)) return false;

        result = new FunctionType(returnType, parameters, type.ClosureModifier is not null);
        //type.SetAnalyzedType(result);
        return true;
    }

    public static bool From(
        TypeInstancePointer type,
        FindType typeFinder,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        ComputeValue? constComputer = null)
    {
        result = null;

        if (!From(type.To, typeFinder, out GeneralType? to, out error, constComputer)) return false;

        result = new PointerType(to);
        //type.SetAnalyzedType(result);

        return true;
    }

    public static bool From(
        TypeInstanceSimple type,
        FindType typeFinder,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        ComputeValue? constComputer = null)
    {
        //result = null;
        error = null;

        if (TypeKeywords.BasicTypes.TryGetValue(type.Identifier.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            //type.SetAnalyzedType(result);
            return true;
        }

        if (!typeFinder.Invoke(type.Identifier, type.File, out result, out error))
        {
            return false;
        }

        if (result.Is(out StructType? resultStructType) &&
            resultStructType.Struct.Template is not null)
        {
            if (type.TypeArguments.HasValue)
            {
                if (!FromArray(type.TypeArguments.Value, typeFinder, out ImmutableArray<GeneralType> typeParameters, out error, constComputer)) return false;
                result = new StructType(resultStructType.Struct, type.File, typeParameters);
            }
            else
            {
                result = new StructType(resultStructType.Struct, type.File);
            }
        }
        else
        {
            if (type.TypeArguments.HasValue)
            {
                error = new($"Asd", type);
                return false;
            }
        }

        //type.SetAnalyzedType(result);
        return true;
    }

    public static ImmutableArray<GeneralType> FromArray<T>(ImmutableArray<T> types) where T : IHaveCompiledType
    {
        ImmutableArray<GeneralType>.Builder result = ImmutableArray.CreateBuilder<GeneralType>(types.Length);
        foreach (IHaveCompiledType item in types) result.Add(item.Type);
        return result.MoveToImmutable();
    }

    public static bool FromArray(
        ImmutableArray<TypeInstance> types,
        FindType typeFinder,
        [NotNullWhen(true)] out ImmutableArray<GeneralType> result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        ComputeValue? constComputer = null)
    {
        result = default;
        error = null;

        ImmutableArray<GeneralType>.Builder _result = ImmutableArray.CreateBuilder<GeneralType>(types.Length);
        foreach (TypeInstance item in types)
        {
            if (!From(item, typeFinder, out GeneralType? _item, out error, constComputer)) return false;
            _result.Add(_item);
        }
        result = _result.MoveToImmutable();
        return true;
    }

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
        PointerType => other == RuntimeType.I32,
        _ => false,
    };

    public static bool TryGetTypeParameters(GeneralType defined, GeneralType passed, Dictionary<string, GeneralType> typeParameters)
    {
        defined = defined.FinalValue;
        passed = passed.FinalValue;

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
            if (definedPointerType.To is ArrayType definedArrayType && passedPointerType.To is ArrayType passedArrayType)
            {
                if (definedArrayType.ComputedLength.HasValue)
                {
                    if (!passedArrayType.ComputedLength.HasValue) return false;
                    if (definedArrayType.ComputedLength.Value != passedArrayType.ComputedLength.Value) return false;
                }

                return TryGetTypeParameters(definedArrayType.Of, passedArrayType.Of, typeParameters);
            }

            return TryGetTypeParameters(definedPointerType.To, passedPointerType.To, typeParameters);
        }

        if (defined.Is(out FunctionType? definedFunctionType) && passed.Is(out FunctionType? passedFunctionType))
        {
            if (definedFunctionType.Parameters.Length != passedFunctionType.Parameters.Length) return false;
            for (int i = 0; i < definedFunctionType.Parameters.Length; i++)
            {
                if (!TryGetTypeParameters(definedFunctionType.Parameters[i], passedFunctionType.Parameters[i], typeParameters)) return false;
            }
            if (!TryGetTypeParameters(definedFunctionType.ReturnType, passedFunctionType.ReturnType, typeParameters)) return false;
            return true;
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

    [return: NotNullIfNotNull(nameof(typeArguments))]
    public static GeneralType? InsertTypeParameters(GeneralType type, IReadOnlyDictionary<string, GeneralType>? typeArguments)
    {
        if (typeArguments is null) return null;

        switch (type)
        {
            case GenericType genericType:
            {
                if (!typeArguments.TryGetValue(genericType.Identifier, out GeneralType? passedTypeArgument))
                { throw new InternalExceptionWithoutContext(); }
                return passedTypeArgument;
            }

            case PointerType pointerType:
            {
                GeneralType pointerTo = InsertTypeParameters(pointerType.To, typeArguments);
                return new PointerType(pointerTo);
            }

            case ArrayType arrayType:
            {
                GeneralType stackArrayOf = InsertTypeParameters(arrayType.Of, typeArguments);
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
                        { return type; }
                    }

                    return new StructType(structType.Struct, structType.File, structTypeParameterValues);
                }

                return type;
            }

            case BuiltinType:
                return type;

            case AliasType: // TODO
                return type;

            case FunctionType functionType:
            {
                GeneralType returnType = InsertTypeParameters(functionType.ReturnType, typeArguments);
                ImmutableArray<GeneralType> parameters = InsertTypeParameters(functionType.Parameters, typeArguments);
                return new FunctionType(returnType, parameters, functionType.HasClosure);
            }

            default:
                throw new NotImplementedException();
        }
    }

    public static ImmutableArray<GeneralType> InsertTypeParameters(ImmutableArray<GeneralType> types, IReadOnlyDictionary<string, GeneralType> typeArguments)
    {
        ImmutableArray<GeneralType>.Builder result = ImmutableArray.CreateBuilder<GeneralType>(types.Length);
        foreach (GeneralType type in types)
        {
            result.Add(InsertTypeParameters(type, typeArguments) ?? type);
        }
        return result.MoveToImmutable();
    }
}
