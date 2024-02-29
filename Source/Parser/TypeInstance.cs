﻿namespace LanguageCore.Parser;

using Compiler;
using Statement;
using Tokenizing;

public abstract class TypeInstance : IEquatable<TypeInstance>, IPositioned
{
    public abstract Position Position { get; }

    public static bool operator ==(TypeInstance? a, string? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a is not TypeInstanceSimple a2) return false;
        if (a2.GenericTypes is not null) return false;
        return a2.Identifier.Content == b;
    }
    public static bool operator !=(TypeInstance? a, string? b) => !(a == b);

    public static bool operator ==(string? a, TypeInstance? b) => b == a;
    public static bool operator !=(string? a, TypeInstance? b) => !(b == a);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is null) return false;
        if (obj is not TypeInstance other) return false;
        return this.Equals(other);
    }

    public abstract bool Equals(TypeInstance? other);

    public abstract override int GetHashCode();
    public abstract override string ToString();
    public virtual string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments) => ToString();

    protected static bool TryGetAnalyzedType(GeneralType type, out TokenAnalyzedType analyzedType)
    {
        analyzedType = default;

        switch (type)
        {
            case StructType:
                analyzedType = TokenAnalyzedType.Struct;
                return true;
            case GenericType:
                analyzedType = TokenAnalyzedType.TypeParameter;
                return true;
            case BuiltinType:
                analyzedType = TokenAnalyzedType.BuiltinType;
                return true;
            case FunctionType functionType:
                return TryGetAnalyzedType(functionType.ReturnType, out analyzedType);
            case EnumType:
                analyzedType = TokenAnalyzedType.Enum;
                return true;
            default:
                return false;
        }
    }

    public abstract void SetAnalyzedType(GeneralType type);
}

public class TypeInstanceStackArray : TypeInstance, IEquatable<TypeInstanceStackArray?>
{
    public StatementWithValue? StackArraySize { get; }
    public TypeInstance StackArrayOf { get; }

    public TypeInstanceStackArray(TypeInstance stackArrayOf, StatementWithValue? sizeValue) : base()
    {
        this.StackArrayOf = stackArrayOf;
        this.StackArraySize = sizeValue;
    }

    public override bool Equals(object? obj) => obj is TypeInstanceStackArray other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstanceStackArray other_ && Equals(other_);
    public bool Equals(TypeInstanceStackArray? other)
    {
        if (other is null) return false;
        if (!this.StackArrayOf.Equals(other.StackArrayOf)) return false;

        if (this.StackArraySize is null != other.StackArraySize is null) return false;

        return true;
    }

    public override int GetHashCode() => HashCode.Combine((byte)1, StackArrayOf, StackArraySize);

    public override Position Position => new(StackArrayOf, StackArraySize);

    public override void SetAnalyzedType(GeneralType type)
    {
        if (type is not ArrayType arrayType) return;

        StackArrayOf.SetAnalyzedType(arrayType.Of);
    }

    public override string ToString() => $"{StackArrayOf}[{StackArraySize}]";
    public override string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments) => $"{StackArrayOf.ToString(typeArguments)}[{StackArraySize}]";
}

public class TypeInstanceFunction : TypeInstance, IEquatable<TypeInstanceFunction?>
{
    public TypeInstance FunctionReturnType { get; }
    public ImmutableArray<TypeInstance> FunctionParameterTypes { get; }
    public override Position Position =>
        new Position(FunctionReturnType)
        .Union(FunctionParameterTypes);

    public TypeInstanceFunction(TypeInstance returnType, IEnumerable<TypeInstance> parameters) : base()
    {
        FunctionReturnType = returnType;
        FunctionParameterTypes = parameters.ToImmutableArray();
    }

    public override bool Equals(object? obj) => obj is TypeInstanceFunction other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstanceFunction other_ && Equals(other_);
    public bool Equals(TypeInstanceFunction? other)
    {
        if (other is null) return false;
        if (!this.FunctionReturnType.Equals(other.FunctionReturnType)) return false;
        if (this.FunctionParameterTypes.Length != other.FunctionParameterTypes.Length) return false;
        for (int i = 0; i < this.FunctionParameterTypes.Length; i++)
        {
            if (!this.FunctionParameterTypes[i].Equals(other.FunctionParameterTypes[i]))
            { return false; }
        }
        return true;
    }

    public override int GetHashCode() => HashCode.Combine((byte)2, FunctionReturnType, FunctionParameterTypes);

    public override void SetAnalyzedType(GeneralType type)
    {
        if (type is not FunctionType functionType) return;

        FunctionReturnType.SetAnalyzedType(functionType.ReturnType);

        if (this.FunctionParameterTypes.Length == functionType.Parameters.Length)
        {
            for (int i = 0; i < functionType.Parameters.Length; i++)
            {
                this.FunctionParameterTypes[i].SetAnalyzedType(functionType.Parameters[i]);
            }
        }
    }

    public override string ToString() => $"{FunctionReturnType}({string.Join<TypeInstance>(", ", FunctionParameterTypes)})";
    public override string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments)
    {
        StringBuilder result = new();
        result.Append(FunctionReturnType.ToString(typeArguments));
        result.Append('(');
        for (int i = 0; i < FunctionParameterTypes.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(FunctionParameterTypes[i].ToString(typeArguments));
        }
        result.Append(')');
        return result.ToString();
    }
}

public class TypeInstanceSimple : TypeInstance, IEquatable<TypeInstanceSimple?>
{
    public Token Identifier { get; }
    public ImmutableArray<TypeInstance>? GenericTypes { get; }
    public override Position Position =>
        new Position(Identifier)
        .Union(GenericTypes);

    public TypeInstanceSimple(Token identifier, IEnumerable<TypeInstance>? genericTypes = null) : base()
    {
        this.Identifier = identifier;
        this.GenericTypes = genericTypes?.ToImmutableArray();
    }

    public override bool Equals(object? obj) => obj is TypeInstanceSimple other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstanceSimple other_ && Equals(other_);
    public bool Equals(TypeInstanceSimple? other)
    {
        if (other is null) return false;
        if (Identifier.Content != other.Identifier.Content) return false;

        if (!GenericTypes.HasValue) return other.GenericTypes is null;
        if (!other.GenericTypes.HasValue) return false;

        if (GenericTypes.Value.Length != other.GenericTypes.Value.Length) return false;
        for (int i = 0; i < GenericTypes.Value.Length; i++)
        {
            if (!GenericTypes.Value[i].Equals(other.GenericTypes.Value[i]))
            { return false; }
        }
        return true;
    }

    public override int GetHashCode() => HashCode.Combine((byte)3, Identifier, GenericTypes);

    public override void SetAnalyzedType(GeneralType type)
    {
        if (TryGetAnalyzedType(type, out TokenAnalyzedType analyzedType))
        { Identifier.AnalyzedType = analyzedType; }
    }

    public static TypeInstanceSimple CreateAnonymous(string name)
        => new(Token.CreateAnonymous(name), null);

    public static TypeInstanceSimple CreateAnonymous(string name, IEnumerable<TypeInstance>? genericTypes)
        => new(Token.CreateAnonymous(name), genericTypes);

    public static TypeInstanceSimple CreateAnonymous(string name, IEnumerable<Token>? genericTypes)
    {
        TypeInstance[]? genericTypesConverted;
        if (genericTypes == null)
        { genericTypesConverted = null; }
        else
        {
            Token[] genericTypesA = genericTypes.ToArray();
            genericTypesConverted = new TypeInstance[genericTypesA.Length];
            for (int i = 0; i < genericTypesA.Length; i++)
            {
                genericTypesConverted[i] = TypeInstanceSimple.CreateAnonymous(genericTypesA[i].Content);
            }
        }

        return new TypeInstanceSimple(Token.CreateAnonymous(name), genericTypesConverted);
    }

    public override string ToString()
    {
        if (GenericTypes is null) return Identifier.Content;
        return $"{Identifier.Content}<{string.Join<TypeInstance>(", ", GenericTypes)}>";
    }
    public override string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments)
    {
        string identifier = Identifier.Content;
        if (typeArguments.TryGetValue(Identifier.Content, out GeneralType? replaced))
        { identifier = replaced.ToString(); }

        if (!GenericTypes.HasValue)
        { return identifier; }

        StringBuilder result = new(identifier);
        result.Append('<');
        for (int i = 0; i < GenericTypes.Value.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(GenericTypes.Value[i].ToString(typeArguments));
        }
        result.Append('>');
        return result.ToString();
    }
}

public class TypeInstancePointer : TypeInstance, IEquatable<TypeInstancePointer?>
{
    public TypeInstance To { get; }
    public Token Operator { get; }
    public override Position Position => new(To, Operator);

    public TypeInstancePointer(TypeInstance to, Token @operator) : base()
    {
        this.To = to;
        this.Operator = @operator;
    }

    public override bool Equals(object? obj) => obj is TypeInstancePointer other && Equals(other);
    public override bool Equals(TypeInstance? other) => other is TypeInstancePointer other_ && Equals(other_);
    public bool Equals(TypeInstancePointer? other)
    {
        if (other is null) return false;
        return this.To.Equals(other.To);
    }

    public override int GetHashCode() => HashCode.Combine((byte)4, To);

    public override void SetAnalyzedType(GeneralType type)
    {
        if (type is not PointerType pointerType) return;
        To.SetAnalyzedType(pointerType.To);
    }

    public override string ToString() => $"{To}{Operator}";
    public override string ToString(IReadOnlyDictionary<string, GeneralType> typeArguments) => $"{To.ToString(typeArguments)}{Operator}";

    public static TypeInstancePointer CreateAnonymous(TypeInstance to) => new(to, Token.CreateAnonymous("*", TokenType.Operator));
}