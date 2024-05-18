namespace LanguageCore.Compiler;

using Parser;
using Runtime;

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

        if (TypeKeywords.BasicTypes.TryGetValue(otherSimple.Identifier.Content, out BasicType type))
        { return Type == type; }

        return false;
    }
    public override int GetHashCode() => HashCode.Combine(Type);
    public override string ToString() => Type switch
    {
        BasicType.Void => TypeKeywords.Void,
        BasicType.Byte => TypeKeywords.Byte,
        BasicType.Integer => TypeKeywords.Int,
        BasicType.Float => TypeKeywords.Float,
        BasicType.Char => TypeKeywords.Char,
        _ => throw new UnreachableException(),
    };

    public override TypeInstance ToTypeInstance() => TypeInstanceSimple.CreateAnonymous(ToString(), null);
}
