using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledBuiltinTypeExpression : CompiledTypeExpression,
    IEquatable<CompiledBuiltinTypeExpression>
{
    public BasicType Type { get; }

    public RuntimeType RuntimeType => Type switch
    {
        BasicType.U8 => RuntimeType.U8,
        BasicType.I8 => RuntimeType.I8,
        BasicType.U16 => RuntimeType.U16,
        BasicType.I16 => RuntimeType.I16,
        BasicType.U32 => RuntimeType.U32,
        BasicType.I32 => RuntimeType.I32,
        BasicType.F32 => RuntimeType.F32,

        _ => throw new NotImplementedException($"Type conversion for \"{Type}\" is not implemented"),
    };

    [SetsRequiredMembers]
    public CompiledBuiltinTypeExpression(BasicType type, Location location) : base(location)
    {
        Type = type;
    }

    public override bool Equals(object? other) => Equals(other as CompiledBuiltinTypeExpression);
    public override bool Equals(CompiledTypeExpression? other) => Equals(other as CompiledBuiltinTypeExpression);
    public bool Equals(CompiledBuiltinTypeExpression? other)
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

        if (TypeKeywords.BasicTypes.TryGetValue(otherSimple.Identifier.Content, out BasicType type))
        { return Type == type; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Type);
    public override string ToString() => Type switch
    {
        BasicType.Void => TypeKeywords.Void,
        BasicType.Any => TypeKeywords.Any,
        BasicType.U8 => TypeKeywords.U8,
        BasicType.I8 => TypeKeywords.I8,
        BasicType.U16 => TypeKeywords.U16,
        BasicType.I16 => TypeKeywords.I16,
        BasicType.U32 => TypeKeywords.U32,
        BasicType.I32 => TypeKeywords.I32,
        BasicType.U64 => TypeKeywords.U64,
        BasicType.I64 => TypeKeywords.I64,
        BasicType.F32 => TypeKeywords.F32,
        _ => throw new UnreachableException(),
    };
    public override string Stringify(int depth = 0) => ToString();

    public static CompiledBuiltinTypeExpression CreateAnonymous(BuiltinType type, ILocated location)
    {
        return new(type.Type, location.Location);
    }
}
