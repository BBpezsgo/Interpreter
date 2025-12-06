using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public abstract class CompiledTypeExpression : CompiledStatement,
    IEquatable<CompiledTypeExpression>,
    IEquatable<TypeInstance>,
    IEquatable<BasicType>,
    IEquatable<RuntimeType>,
    ILocated,
    IPositioned,
    IInFile
{
    public virtual CompiledTypeExpression FinalValue => this;

    [SetsRequiredMembers]
    public CompiledTypeExpression(Location location)
    {
        Location = location;
    }

    public static CompiledTypeExpression CreateAnonymous(GeneralType type, ILocated location)
    {
        return type switch
        {
            AliasType v => CompiledAliasTypeExpression.CreateAnonymous(v, location),
            ArrayType v => CompiledArrayTypeExpression.CreateAnonymous(v, location),
            BuiltinType v => CompiledBuiltinTypeExpression.CreateAnonymous(v, location),
            FunctionType v => CompiledFunctionTypeExpression.CreateAnonymous(v, location),
            GenericType v => CompiledGenericTypeExpression.CreateAnonymous(v, location),
            PointerType v => CompiledPointerTypeExpression.CreateAnonymous(v, location),
            StructType v => CompiledStructTypeExpression.CreateAnonymous(v, location),
            _ => throw new UnreachableException(),
        };
    }

    [Obsolete]
    public static bool operator ==(CompiledTypeExpression? a, CompiledTypeExpression? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        return a.Equals(b);
    }
    [Obsolete]
    public static bool operator !=(CompiledTypeExpression? a, CompiledTypeExpression? b) => !(a == b);

    [Obsolete]
    public static bool operator ==([NotNullWhen(true)] CompiledTypeExpression? a, RuntimeType b) => a is not null && a.Equals(b);
    [Obsolete]
    public static bool operator !=(CompiledTypeExpression? a, RuntimeType b) => !(a == b);

    [Obsolete]
    public static bool operator ==([NotNullWhen(true)] CompiledTypeExpression? a, BasicType b) => a is not null && a.Equals(b);
    [Obsolete]
    public static bool operator !=(CompiledTypeExpression? a, BasicType b) => !(a == b);

    public bool SameAs([NotNullWhen(true)] CompiledTypeExpression? other)
    {
        if (other is null) return false;
        return FinalValue.Equals(other.FinalValue);
    }
    public bool SameAs(BasicType other) => FinalValue.Equals(other);

    public bool Is<T>([NotNullWhen(true)] out T? value) where T : CompiledTypeExpression
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
    public abstract bool Equals(CompiledTypeExpression? other);
    public abstract bool Equals(TypeInstance? other);
    public abstract override int GetHashCode();
    public abstract override string ToString();
    public bool Equals(BasicType other) => FinalValue is CompiledBuiltinTypeExpression builtinType && builtinType.Type == other;
    public bool Equals(RuntimeType other) => FinalValue switch
    {
        CompiledBuiltinTypeExpression builtinType => builtinType.RuntimeType == other,
        CompiledPointerTypeExpression => other == RuntimeType.I32,
        _ => false,
    };

    public static bool TryGetTypeParameters(CompiledTypeExpression defined, CompiledTypeExpression passed, Dictionary<string, CompiledTypeExpression> typeParameters)
    {
        if (passed.Is(out CompiledGenericTypeExpression? passedGenericType))
        {
            if (typeParameters.ContainsKey(passedGenericType.Identifier))
            { return false; }
            throw new NotImplementedException($"This should be non-generic");
        }

        if (defined.Is(out CompiledGenericTypeExpression? definedGenericType))
        {
            if (typeParameters.TryGetValue(definedGenericType.Identifier, out CompiledTypeExpression? addedTypeParameter))
            {
                if (!addedTypeParameter.SameAs(passed)) return false;
            }
            else
            {
                typeParameters.Add(definedGenericType.Identifier, passed);
            }

            return true;
        }

        if (defined.Is(out CompiledPointerTypeExpression? definedPointerType) && passed.Is(out CompiledPointerTypeExpression? passedPointerType))
        {
            if (definedPointerType.To is CompiledArrayTypeExpression definedArrayType && passedPointerType.To is CompiledArrayTypeExpression passedArrayType)
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

        if (defined.Is(out CompiledFunctionTypeExpression? definedFunctionType) && passed.Is(out CompiledFunctionTypeExpression? passedFunctionType))
        {
            if (definedFunctionType.Parameters.Length != passedFunctionType.Parameters.Length) return false;
            for (int i = 0; i < definedFunctionType.Parameters.Length; i++)
            {
                if (!TryGetTypeParameters(definedFunctionType.Parameters[i], passedFunctionType.Parameters[i], typeParameters)) return false;
            }
            if (!TryGetTypeParameters(definedFunctionType.ReturnType, passedFunctionType.ReturnType, typeParameters)) return false;
            return true;
        }

        if (defined.Is(out CompiledStructTypeExpression? definedStructType) && passed.Is(out CompiledStructTypeExpression? passedStructType))
        {
            if (definedStructType.Struct.Identifier.Content != passedStructType.Struct.Identifier.Content) return false;
            if (definedStructType.Struct.Template is not null && passedStructType.Struct.Template is not null)
            {
                if (definedStructType.Struct.Template.Parameters.Length != passedStructType.TypeArguments.Count)
                { throw new NotImplementedException(); }

                for (int i = 0; i < definedStructType.Struct.Template.Parameters.Length; i++)
                {
                    string typeParamName = definedStructType.Struct.Template.Parameters[i].Content;
                    CompiledTypeExpression typeParamValue = passedStructType.TypeArguments[typeParamName];

                    if (typeParameters.TryGetValue(typeParamName, out CompiledTypeExpression? addedTypeParameter))
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
            case CompiledGenericTypeExpression: return false;
            case CompiledBuiltinTypeExpression: return true;
            case CompiledPointerTypeExpression pointerType: return pointerType.To.AllGenericsDefined();
            case CompiledArrayTypeExpression arrayType: return arrayType.Of.AllGenericsDefined();

            case CompiledFunctionTypeExpression functionType:
            {
                for (int i = 0; i < functionType.Parameters.Length; i++)
                {
                    if (!functionType.Parameters[i].AllGenericsDefined())
                    { return false; }
                }
                return functionType.ReturnType.AllGenericsDefined();
            }

            case CompiledStructTypeExpression structType:
            {
                if (structType.Struct.Template is null) return true;
                return structType.TypeArguments is not null;
            }

            default: throw new NotImplementedException();
        }
    }

    [return: NotNullIfNotNull(nameof(typeArguments))]
    public static CompiledTypeExpression? InsertTypeParameters(CompiledTypeExpression type, IReadOnlyDictionary<string, CompiledTypeExpression>? typeArguments)
    {
        if (typeArguments is null) return null;

        switch (type)
        {
            case CompiledGenericTypeExpression genericType:
            {
                if (!typeArguments.TryGetValue(genericType.Identifier, out CompiledTypeExpression? passedTypeArgument))
                { throw new InternalExceptionWithoutContext(); }
                return passedTypeArgument;
            }

            case CompiledPointerTypeExpression pointerType:
            {
                CompiledTypeExpression pointerTo = InsertTypeParameters(pointerType.To, typeArguments);
                return new CompiledPointerTypeExpression(pointerTo, type.Location);
            }

            case CompiledArrayTypeExpression arrayType:
            {
                CompiledTypeExpression stackArrayOf = InsertTypeParameters(arrayType.Of, typeArguments);
                return new CompiledArrayTypeExpression(stackArrayOf, arrayType.Length, type.Location);
            }

            case CompiledStructTypeExpression structType:
            {
                if (structType.Struct.Template is not null)
                {
                    CompiledTypeExpression[] structTypeParameterValues = new CompiledTypeExpression[structType.Struct.Template.Parameters.Length];

                    foreach (KeyValuePair<string, CompiledTypeExpression> item in typeArguments)
                    {
                        if (structType.Struct.TryGetTypeArgumentIndex(item.Key, out int i))
                        { structTypeParameterValues[i] = item.Value; }
                    }

                    for (int i = 0; i < structTypeParameterValues.Length; i++)
                    {
                        if (structTypeParameterValues[i] is null or CompiledGenericTypeExpression)
                        { return type; }
                    }

                    return new CompiledStructTypeExpression(structType.Struct, structType.File, structTypeParameterValues, type.Location);
                }

                return type;
            }

            case CompiledBuiltinTypeExpression:
                return type;

            case CompiledAliasTypeExpression: // TODO
                return type;

            case CompiledFunctionTypeExpression functionType:
            {
                CompiledTypeExpression returnType = InsertTypeParameters(functionType.ReturnType, typeArguments);
                ImmutableArray<CompiledTypeExpression> parameters = InsertTypeParameters(functionType.Parameters, typeArguments);
                return new CompiledFunctionTypeExpression(returnType, parameters, functionType.HasClosure, type.Location);
            }

            default:
                throw new NotImplementedException();
        }
    }

    public static ImmutableArray<CompiledTypeExpression> InsertTypeParameters(ImmutableArray<CompiledTypeExpression> types, IReadOnlyDictionary<string, CompiledTypeExpression> typeArguments)
    {
        ImmutableArray<CompiledTypeExpression>.Builder result = ImmutableArray.CreateBuilder<CompiledTypeExpression>(types.Length);
        foreach (CompiledTypeExpression type in types)
        {
            result.Add(InsertTypeParameters(type, typeArguments) ?? type);
        }
        return result.MoveToImmutable();
    }
}
