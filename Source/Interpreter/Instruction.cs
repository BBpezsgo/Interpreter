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
    _8,
    _16,
    _32,
}

public enum InstructionOperandType : byte
{
    Immediate8,
    Immediate16,
    Immediate32,

    Pointer,

    Register,

    PointerBP,
    PointerSP,

    PointerEAX,
    PointerEBX,
    PointerECX,
    PointerEDX,
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
        InstructionOperandType.Pointer => BitWidth._32,
        InstructionOperandType.Register => (Register)Value.Int switch
        {
            Register.CodePointer => BitWidth._32,
            Register.StackPointer => BitWidth._32,
            Register.BasePointer => BitWidth._32,
            Register.EAX => BitWidth._32,
            Register.AX => BitWidth._16,
            Register.AH => BitWidth._8,
            Register.AL => BitWidth._8,
            Register.EBX => BitWidth._32,
            Register.BX => BitWidth._16,
            Register.BH => BitWidth._8,
            Register.BL => BitWidth._8,
            Register.ECX => BitWidth._32,
            Register.CX => BitWidth._16,
            Register.CH => BitWidth._8,
            Register.CL => BitWidth._8,
            Register.EDX => BitWidth._32,
            Register.DX => BitWidth._16,
            Register.DH => BitWidth._8,
            Register.DL => BitWidth._8,
            _ => throw new UnreachableException(),
        },
        InstructionOperandType.PointerBP => BitWidth._32,
        InstructionOperandType.PointerSP => BitWidth._32,
        InstructionOperandType.PointerEAX => BitWidth._32,
        InstructionOperandType.PointerEBX => BitWidth._32,
        InstructionOperandType.PointerECX => BitWidth._32,
        InstructionOperandType.PointerEDX => BitWidth._32,
        _ => throw new UnreachableException(),
    };

    public InstructionOperand(RuntimeValue value, InstructionOperandType type)
    {
        Value = value;
        Type = type;
    }

    public override string ToString() => Type switch
    {
        InstructionOperandType.Immediate8 => Value.ToString(),
        InstructionOperandType.Immediate16 => Value.ToString(),
        InstructionOperandType.Immediate32 => Value.ToString(),
        InstructionOperandType.Pointer => $"[{Value}]",
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
        InstructionOperandType.PointerBP => $"[BP{(Value.Int == 0 ? null : Value.Int > 0 ? $"+{Value.Int}" : $"-{Value.Int}")}]",
        InstructionOperandType.PointerSP => $"[SP{(Value.Int == 0 ? null : Value.Int > 0 ? $"+{Value.Int}" : $"-{Value.Int}")}]",
        InstructionOperandType.PointerEAX => $"[EAX{(Value.Int == 0 ? null : Value.Int > 0 ? $"+{Value.Int}" : $"-{Value.Int}")}]",
        InstructionOperandType.PointerEBX => $"[EBX{(Value.Int == 0 ? null : Value.Int > 0 ? $"+{Value.Int}" : $"-{Value.Int}")}]",
        InstructionOperandType.PointerECX => $"[ECX{(Value.Int == 0 ? null : Value.Int > 0 ? $"+{Value.Int}" : $"-{Value.Int}")}]",
        InstructionOperandType.PointerEDX => $"[EDX{(Value.Int == 0 ? null : Value.Int > 0 ? $"+{Value.Int}" : $"-{Value.Int}")}]",
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
            AddressingMode.Pointer => new InstructionOperand(new RuntimeValue(address.Address), InstructionOperandType.Pointer),
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
