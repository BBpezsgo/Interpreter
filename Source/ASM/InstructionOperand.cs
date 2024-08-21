using LanguageCore.Compiler;

namespace LanguageCore.ASM;

public enum InstructionOperandType
{
    None,
    Immediate,
    Register,
    DirectAddress,
    IndirectAddress,
    IndexedAddress,
    BasedAddress,
    BasedIndexedAddress,
    Label,
}

[ExcludeFromCodeCoverage]
public readonly struct InstructionOperand : IEquatable<InstructionOperand>
{
    public readonly InstructionOperandType Type;
    readonly ushort _1;
    readonly ushort _2;
    readonly string? _raw;

    InstructionOperand(InstructionOperandType type, ushort _1 = default, ushort _2 = default)
    {
        Type = type;
        this._1 = _1;
        this._2 = _2;
        _raw = null;
    }

    InstructionOperand(InstructionOperandType type, string _raw)
    {
        Type = type;
        _1 = default;
        _2 = default;
        this._raw = _raw;
    }

    public override string ToString() => Type switch
    {
        InstructionOperandType.Immediate => $"DWORD {_1}",
        InstructionOperandType.Register => $"{(Intel.Register)_1}",
        InstructionOperandType.DirectAddress => $"DWORD [{_1}]",
        InstructionOperandType.IndirectAddress => $"DWORD [{(Intel.Register)_1}]",
        InstructionOperandType.IndexedAddress => $"DWORD [{(Intel.Register)_1}+{(Intel.Register)_2}]",
        InstructionOperandType.BasedAddress => throw new NotImplementedException(),
        InstructionOperandType.BasedIndexedAddress => throw new NotImplementedException(),
        InstructionOperandType.None => throw new NotImplementedException(),
        InstructionOperandType.Label => _raw ?? throw new NullReferenceException(),
        _ => throw new NotImplementedException(),
    };

    public override bool Equals(object? obj) =>
        obj is InstructionOperand operand &&
        Equals(operand);

    public bool Equals(InstructionOperand other) =>
        Type == other.Type &&
        _1 == other._1 &&
        _2 == other._2;

    public override int GetHashCode() => HashCode.Combine(Type, _1, _2);

    public static bool operator ==(InstructionOperand left, InstructionOperand right) => left.Equals(right);
    public static bool operator !=(InstructionOperand left, InstructionOperand right) => !left.Equals(right);

    public static implicit operator InstructionOperand(int immediate) => new(InstructionOperandType.Immediate, checked((ushort)immediate));
    public static implicit operator InstructionOperand(Intel.Register register) => new(InstructionOperandType.Register, (ushort)register);
    public static InstructionOperand Pointer(int directAddress) => new(InstructionOperandType.DirectAddress, checked((ushort)directAddress));
    public static InstructionOperand Pointer(Intel.Register indirectAddress) => new(InstructionOperandType.IndirectAddress, (ushort)indirectAddress);
    public static InstructionOperand Pointer(Intel.Register baseAddress, int offset) => new(InstructionOperandType.IndexedAddress, (ushort)baseAddress, checked((ushort)offset));
    public static InstructionOperand Label(string label) => new(InstructionOperandType.Label, label);

    public static explicit operator InstructionOperand(Runtime.RuntimeValue v) => new(InstructionOperandType.Immediate, v.U16);
    public static explicit operator InstructionOperand(CompiledValue v) => new(InstructionOperandType.Immediate, v.Char);
}
