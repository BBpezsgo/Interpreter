namespace LanguageCore.Runtime;

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
    Immediate,
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
    public readonly DataItem Value;
    public readonly InstructionOperandType Type;

    public BitWidth BitWidth => Type switch
    {
        InstructionOperandType.Immediate => Value.Type.Convert().ToBitWidth(),
        InstructionOperandType.Pointer => Value.Type.Convert().ToBitWidth(),
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

    public InstructionOperand(DataItem value, InstructionOperandType type)
    {
        Value = value;
        Type = type;
    }

    public override string ToString() => Type switch
    {
        InstructionOperandType.Immediate => Value.ToString(),
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

    public static implicit operator InstructionOperand(DataItem value) => new(value, InstructionOperandType.Immediate);
    public static implicit operator InstructionOperand(int value) => new(new DataItem(value), InstructionOperandType.Immediate);
    public static implicit operator InstructionOperand(Register register) => new((int)register, InstructionOperandType.Register);
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

[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
public readonly struct Instruction
{
    public readonly Opcode Opcode;
    public readonly BitWidth BitWidth
    {
        get
        {
            if (Operand1.Value.IsNull &&
                Operand2.Value.IsNull)
            { return BitWidth._32; }

            if (!Operand1.Value.IsNull)
            { return Operand1.BitWidth; }

            if (!Operand2.Value.IsNull)
            { return Operand2.BitWidth; }

            BitWidth _1 = Operand1.BitWidth;
            BitWidth _2 = Operand2.BitWidth;
            return (_1 > _2) ? _1 : _2;
        }
    }
    public readonly InstructionOperand Operand1;
    public readonly InstructionOperand Operand2;

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

        if (!Operand1.Value.IsNull)
        {
            result.Append(' ');
            result.Append(Operand1.ToString());
        }

        if (!Operand2.Value.IsNull)
        {
            result.Append(' ');
            result.Append(Operand2.ToString());
        }

        return result.ToString();
    }
}
