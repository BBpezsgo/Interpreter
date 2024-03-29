﻿namespace LanguageCore.Runtime;

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

[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
public readonly struct Instruction
{
    public readonly AddressingMode AddressingMode;
    public readonly Opcode Opcode;
    public readonly DataItem Parameter;

    public Instruction(Opcode opcode, DataItem parameter)
    {
        Opcode = opcode;
        AddressingMode = AddressingMode.Absolute;
        Parameter = parameter;
    }

    public Instruction(Opcode opcode, AddressingMode addressingMode = AddressingMode.Absolute)
    {
        Opcode = opcode;
        AddressingMode = addressingMode;
        Parameter = DataItem.Null;
    }

    public Instruction(Opcode opcode, AddressingMode addressingMode, DataItem parameter)
    {
        Opcode = opcode;
        AddressingMode = addressingMode;
        Parameter = parameter;
    }

    public Instruction(PreparationInstruction other)
    {
        Opcode = other.Opcode;
        AddressingMode = other.AddressingMode;
        Parameter = other.Parameter;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Opcode.ToString());

        if (Opcode == Opcode.StackLoad ||
            Opcode == Opcode.StackStore)
        {
            result.Append(' ');
            result.Append(AddressingMode.ToString());
        }

        if (!Parameter.IsNull)
        { result.Append($" {{ {Parameter} }}"); }

        return result.ToString();
    }
}
