using LanguageCore.Runtime;
using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public enum BasicType : byte
{
    Void,
    Any,
    U8,
    I8,
    U16,
    I16,
    U32,
    I32,
    U64,
    I64,
    F32,
}

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public class BuiltinType : GeneralType,
    IEquatable<BuiltinType>
{
    public static readonly BuiltinType U8 = new(BasicType.U8);
    public static readonly BuiltinType I8 = new(BasicType.I8);
    public static readonly BuiltinType Char = new(BasicType.U16);
    public static readonly BuiltinType I16 = new(BasicType.I16);
    public static readonly BuiltinType U32 = new(BasicType.U32);
    public static readonly BuiltinType I32 = new(BasicType.I32);
    public static readonly BuiltinType U64 = new(BasicType.U64);
    public static readonly BuiltinType I64 = new(BasicType.I64);
    public static readonly BuiltinType F32 = new(BasicType.F32);
    public static readonly BuiltinType Void = new(BasicType.Void);
    public static readonly BuiltinType Any = new(BasicType.Any);

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

    public BuiltinType(BasicType type)
    {
        Type = type;
    }

    public override bool GetSize(IRuntimeInfoProvider runtime, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        error = default;

        switch (Type)
        {
            case BasicType.Void: error = new PossibleDiagnostic($"Can't get the size of type \"{this}\""); return false;
            case BasicType.Any: error = new PossibleDiagnostic($"Can't get the size of type \"{this}\""); return false;
            case BasicType.U8: size = 1; return true;
            case BasicType.I8: size = 1; return true;
            case BasicType.U16: size = 2; return true;
            case BasicType.I16: size = 2; return true;
            case BasicType.U32: size = 4; return true;
            case BasicType.I32: size = 4; return true;
            case BasicType.U64: size = 8; return true;
            case BasicType.I64: size = 8; return true;
            case BasicType.F32: size = 4; return true;
            default: throw new UnreachableException();
        }
    }

    public override bool GetBitWidth(IRuntimeInfoProvider runtime, out BitWidth bitWidth, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        bitWidth = default;
        if (!GetSize(runtime, out int size, out error))
        {
            return false;
        }
        bitWidth = (BitWidth)size;
        return true;
    }

    public static BuiltinType CreateNumeric(NumericType type, BitWidth size) => type switch
    {
        NumericType.UnsignedInteger => size switch
        {
            BitWidth._8 => U8,
            BitWidth._16 => Char,
            BitWidth._32 => U32,
            BitWidth._64 => U64,
            _ => throw new UnreachableException(),
        },
        NumericType.SignedInteger => size switch
        {
            BitWidth._8 => I8,
            BitWidth._16 => I16,
            BitWidth._32 => I32,
            BitWidth._64 => I64,
            _ => throw new UnreachableException(),
        },
        NumericType.Float => size switch
        {
            BitWidth._8 => throw new NotImplementedException(),
            BitWidth._16 => throw new NotImplementedException(),
            BitWidth._32 => F32,
            BitWidth._64 => throw new NotImplementedException(),
            _ => throw new UnreachableException(),
        },
        _ => throw new UnreachableException(),
    };

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
}
