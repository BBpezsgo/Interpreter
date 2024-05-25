namespace LanguageCore.Runtime;

using Compiler;

public enum AddressingMode : byte
{
    /// <summary>
    /// <c>Parameter</c>
    /// </summary>
    Absolute,

    /// <summary>
    /// <c>Pop()</c>
    /// </summary>
    Runtime,

    /// <summary>
    /// <b>Only for stack!</b>
    /// <br/>
    /// <c>BasePointer + Parameter</c>
    /// </summary>
    BasePointerRelative,

    /// <summary>
    /// <b>Only for stack!</b>
    /// <br/>
    /// <c>StackPointer + Parameter</c>
    /// </summary>
    StackPointerRelative,
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
        InstructionOperandType.Register => Value.Int switch
        {
            RegisterIds.CodePointer => "CP",
            RegisterIds.StackPointer => "SP",
            RegisterIds.BasePointer => "BP",
            RegisterIds.EAX => "EAX",
            RegisterIds.AX => "AX",
            RegisterIds.AH => "AH",
            RegisterIds.AL => "AL",
            RegisterIds.EBX => "EBX",
            RegisterIds.BX => "BX",
            RegisterIds.BH => "BH",
            RegisterIds.BL => "BL",
            RegisterIds.ECX => "ECX",
            RegisterIds.CX => "CX",
            RegisterIds.CH => "CH",
            RegisterIds.CL => "CL",
            RegisterIds.EDX => "EDX",
            RegisterIds.DX => "DX",
            RegisterIds.DH => "DH",
            RegisterIds.DL => "DL",
            _ => throw new UnreachableException(),
        },
        InstructionOperandType.PointerBP => "[BP]",
        InstructionOperandType.PointerSP => "[SP]",
        InstructionOperandType.PointerEAX => "[EAX]",
        InstructionOperandType.PointerEBX => "[EBX]",
        InstructionOperandType.PointerECX => "[ECX]",
        InstructionOperandType.PointerEDX => "[EDX]",
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
        if (address.InHeap) throw new NotImplementedException();
        if (address.IsReference) throw new NotImplementedException();
        return address.AddressingMode switch
        {
            AddressingMode.Absolute => new InstructionOperand(new RuntimeValue(address.Address), InstructionOperandType.Pointer),
            AddressingMode.Runtime => throw new NotImplementedException(),
            AddressingMode.BasePointerRelative => Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection),
            AddressingMode.StackPointerRelative => Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection),
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

public static class RegisterExtensions
{
    public static InstructionOperand ToPtr(this Register register, int offset = 0) => register switch
    {
        Register.StackPointer => new InstructionOperand(offset, InstructionOperandType.PointerSP),
        Register.BasePointer => new InstructionOperand(offset, InstructionOperandType.PointerBP),
        Register.EAX => new InstructionOperand(offset, InstructionOperandType.PointerEAX),
        Register.EBX => new InstructionOperand(offset, InstructionOperandType.PointerEBX),
        Register.ECX => new InstructionOperand(offset, InstructionOperandType.PointerECX),
        Register.EDX => new InstructionOperand(offset, InstructionOperandType.PointerEDX),
        _ => throw new NotImplementedException(),
    };
}

public static class OpcodeExtensions
{
    public static int ParameterCount(this Opcode opcode) => opcode switch
    {
        Opcode.NOP => 0,
        Opcode.Push => 1,
        Opcode.Pop => 0,
        Opcode.PopTo => 1,
        Opcode.Exit => 0,
        Opcode.Call => 1,
        Opcode.Return => 0,
        Opcode.JumpIfEqual => 1,
        Opcode.JumpIfNotEqual => 1,
        Opcode.JumpIfGreater => 1,
        Opcode.JumpIfGreaterOrEqual => 1,
        Opcode.JumpIfLess => 1,
        Opcode.JumpIfLessOrEqual => 1,
        Opcode.Jump => 1,
        Opcode.CallExternal => 1,
        Opcode.Throw => 1,
        Opcode.Compare => 2,
        Opcode.LogicOR => 2,
        Opcode.LogicAND => 2,
        Opcode.BitsAND => 2,
        Opcode.BitsOR => 2,
        Opcode.BitsXOR => 2,
        Opcode.BitsNOT => 1,
        Opcode.BitsShiftLeft => 2,
        Opcode.BitsShiftRight => 2,
        Opcode.MathAdd => 2,
        Opcode.MathSub => 2,
        Opcode.MathMult => 2,
        Opcode.MathDiv => 2,
        Opcode.MathMod => 2,
        Opcode.FMathAdd => 2,
        Opcode.FMathSub => 2,
        Opcode.FMathMult => 2,
        Opcode.FMathDiv => 2,
        Opcode.FMathMod => 2,
        Opcode.Move => 2,
        Opcode.FTo => 1,
        Opcode.FFrom => 1,
        _ => throw new UnreachableException(),
    };
}
