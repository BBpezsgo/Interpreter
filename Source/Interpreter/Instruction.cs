namespace LanguageCore.Runtime;

using Compiler;

public enum AddressingMode : byte
{
    Pointer,
    PointerBP,
    PointerSP,
}

public enum BitWidth : byte
{
    _8 = 1,
    _16 = 2,
    _32 = 4,
}

public enum InstructionOperandType : byte
{
    Immediate8,
    Immediate16,
    Immediate32,

    Pointer8,
    Pointer16,
    Pointer32,

    Register,

    PointerBP8,
    PointerBP16,
    PointerBP32,

    PointerSP8,
    PointerSP16,
    PointerSP32,

    PointerEAX8,
    PointerEAX16,
    PointerEAX32,

    PointerEBX8,
    PointerEBX16,
    PointerEBX32,

    PointerECX8,
    PointerECX16,
    PointerECX32,

    PointerEDX8,
    PointerEDX16,
    PointerEDX32,
}

public readonly struct InstructionOperand
{
    public readonly RuntimeValue Value;
    public readonly InstructionOperandType Type;

    public BitWidth BitWidth => Type switch
    {
        InstructionOperandType.Immediate8 => BitWidth._8,
        InstructionOperandType.Immediate16 => BitWidth._16,
        InstructionOperandType.Immediate32 => BitWidth._32,
        InstructionOperandType.Register => ((Register)Value.Int).BitWidth(),
        InstructionOperandType.Pointer8 => BitWidth._8,
        InstructionOperandType.Pointer16 => BitWidth._16,
        InstructionOperandType.Pointer32 => BitWidth._32,
        InstructionOperandType.PointerBP8 => BitWidth._8,
        InstructionOperandType.PointerBP16 => BitWidth._16,
        InstructionOperandType.PointerBP32 => BitWidth._32,
        InstructionOperandType.PointerSP8 => BitWidth._8,
        InstructionOperandType.PointerSP16 => BitWidth._16,
        InstructionOperandType.PointerSP32 => BitWidth._32,
        InstructionOperandType.PointerEAX8 => BitWidth._8,
        InstructionOperandType.PointerEAX16 => BitWidth._16,
        InstructionOperandType.PointerEAX32 => BitWidth._32,
        InstructionOperandType.PointerEBX8 => BitWidth._8,
        InstructionOperandType.PointerEBX16 => BitWidth._16,
        InstructionOperandType.PointerEBX32 => BitWidth._32,
        InstructionOperandType.PointerECX8 => BitWidth._8,
        InstructionOperandType.PointerECX16 => BitWidth._16,
        InstructionOperandType.PointerECX32 => BitWidth._32,
        InstructionOperandType.PointerEDX8 => BitWidth._8,
        InstructionOperandType.PointerEDX16 => BitWidth._16,
        InstructionOperandType.PointerEDX32 => BitWidth._32,
        _ => throw new UnreachableException(),
    };

    public InstructionOperand(RuntimeValue value, InstructionOperandType type)
    {
        Value = value;
        Type = type;
    }

    static string? PointerOffsetString(int offset) => offset == 0 ? null : offset > 0 ? $"+{offset}" : $"-{offset}";

    [ExcludeFromCodeCoverage]
    public override string ToString() => Type switch
    {
        InstructionOperandType.Immediate8 => Value.ToString(),
        InstructionOperandType.Immediate16 => Value.ToString(),
        InstructionOperandType.Immediate32 => Value.ToString(),
        InstructionOperandType.Pointer8 => $"BYTE [{Value}]",
        InstructionOperandType.Pointer16 => $"WORD [{Value}]",
        InstructionOperandType.Pointer32 => $"DWORD [{Value}]",
        InstructionOperandType.Register => (Register)Value.Int switch
        {
            Register.CodePointer => "CP",
            Register.StackPointer => "SP",
            Register.BasePointer => "BP",
            Register.EAX => "EAX",
            Register.AX => "AX",
            Register.AH => "AH",
            Register.AL => "AL",
            Register.EBX => "EBX",
            Register.BX => "BX",
            Register.BH => "BH",
            Register.BL => "BL",
            Register.ECX => "ECX",
            Register.CX => "CX",
            Register.CH => "CH",
            Register.CL => "CL",
            Register.EDX => "EDX",
            Register.DX => "DX",
            Register.DH => "DH",
            Register.DL => "DL",
            _ => throw new UnreachableException(),
        },
        InstructionOperandType.PointerBP8 => $"BYTE [BP{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerBP16 => $"WORD [BP{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerBP32 => $"DWORD [BP{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerSP8 => $"BYTE [SP{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerSP16 => $"WORD [SP{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerSP32 => $"DWORD [SP{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEAX8 => $"BYTE [EAX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEAX16 => $"WORD [EAX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEAX32 => $"DWORD [EAX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEBX8 => $"BYTE [EBX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEBX16 => $"WORD [EBX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEBX32 => $"DWORD [EBX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerECX8 => $"BYTE [ECX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerECX16 => $"WORD [ECX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerECX32 => $"DWORD [ECX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEDX8 => $"BYTE [EDX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEDX16 => $"WORD [EDX{PointerOffsetString(Value.Int)}]",
        InstructionOperandType.PointerEDX32 => $"DWORD [EDX{PointerOffsetString(Value.Int)}]",
        _ => throw new UnreachableException(),
    };

    public static implicit operator InstructionOperand(CompiledValue value) => value.Type switch
    {
        RuntimeType.Null => new InstructionOperand(default, InstructionOperandType.Immediate32),
        RuntimeType.Byte => new InstructionOperand(value.Byte, InstructionOperandType.Immediate8),
        RuntimeType.Char => new InstructionOperand(value.Char, InstructionOperandType.Immediate16),
        RuntimeType.Integer => new InstructionOperand(value.Int, InstructionOperandType.Immediate32),
        RuntimeType.Single => new InstructionOperand(new RuntimeValue(value.Single), InstructionOperandType.Immediate32),
        _ => throw new UnreachableException(),
    };
    public static implicit operator InstructionOperand(int value) => new(new RuntimeValue(value), InstructionOperandType.Immediate32);
    public static implicit operator InstructionOperand(Register register) => new((int)register, InstructionOperandType.Register);
    public static explicit operator InstructionOperand(ValueAddress address)
    {
        if (address.IsReference) throw new NotImplementedException();
        return address.AddressingMode switch
        {
            AddressingMode.Pointer => new InstructionOperand(new RuntimeValue(address.Address), InstructionOperandType.Pointer32),
            AddressingMode.PointerBP => Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection),
            AddressingMode.PointerSP => Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection),
            _ => throw new UnreachableException(),
        };
    }
}

[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
public readonly struct Instruction
{
    public readonly Opcode Opcode;
    public readonly InstructionOperand Operand1;
    public readonly InstructionOperand Operand2;

    public BitWidth BitWidth
    {
        get
        {
            switch (Opcode.ParameterCount())
            {
                case 0: return BitWidth._32;
                case 1: return Operand1.BitWidth;
                case 2:
                {
                    BitWidth _1 = Operand1.BitWidth;
                    BitWidth _2 = Operand2.BitWidth;
                    return (_1 > _2) ? _1 : _2;
                }
                default: throw new UnreachableException();
            }
        }
    }

    public Instruction(PreparationInstruction other)
    {
        Opcode = other.Opcode;
        Operand1 = other.Operand1;
        Operand2 = other.Operand2;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Opcode.ToString());

        int parameterCount = Opcode.ParameterCount();

        if (parameterCount >= 1)
        {
            result.Append(' ');
            result.Append(Operand1.ToString());
        }

        if (parameterCount >= 2)
        {
            result.Append(' ');
            result.Append(Operand2.ToString());
        }

        return result.ToString();
    }
}
