namespace LanguageCore.Compiler;

using Runtime;
using Parser;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class BuiltinType : GeneralType,
    IEquatable<BuiltinType>
{
    public static readonly BuiltinType Byte = new(BasicType.Byte);
    public static readonly BuiltinType Char = new(BasicType.Char);
    public static readonly BuiltinType Integer = new(BasicType.Integer);
    public static readonly BuiltinType Float = new(BasicType.Float);
    public static readonly BuiltinType Void = new(BasicType.Void);
    public static readonly BuiltinType Any = new(BasicType.Any);

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
    public override int SizeBytes => Type switch
    {
        BasicType.Void => throw new InternalException($"Type {this} does not have a size"),
        BasicType.Any => throw new InternalException($"Type {this} does not have a size"),
        BasicType.Byte => 1,
        BasicType.Char => 2,
        BasicType.Integer => 4,
        BasicType.Float => 4,
        _ => throw new UnreachableException(),
    };

    public override BitWidth BitWidth => SizeBytes switch
    {
        1 => BitWidth._8,
        2 => BitWidth._16,
        4 => BitWidth._32,
        _ => throw new NotImplementedException(),
    };

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

        if (TypeKeywords.BasicTypes.TryGetValue(otherSimple.Identifier.Content, out BasicType type))
        { return Type == type; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Type);
    public override string ToString() => Type switch
    {
        BasicType.Void => TypeKeywords.Void,
        BasicType.Any => TypeKeywords.Any,
        BasicType.Byte => TypeKeywords.U8,
        BasicType.Char => TypeKeywords.U16,
        BasicType.Integer => TypeKeywords.I32,
        BasicType.Float => TypeKeywords.F32,
        _ => throw new UnreachableException(),
    };
}
