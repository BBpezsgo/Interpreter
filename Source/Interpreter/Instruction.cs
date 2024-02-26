using System;
using System.Diagnostics;
using System.Text;

namespace LanguageCore.Runtime;

public enum AddressingMode : byte
{
    /// <summary>
    /// <c>CurrentInstruction.ParameterInt</c>
    /// </summary>
    Absolute,

    /// <summary>
    /// <c>Memory.Stack.Pop().ToInt32(null)</c>
    /// </summary>
    Runtime,

    /// <summary>
    /// <b>Only for stack!</b>
    /// <br/>
    /// <c>BasePointer + CurrentInstruction.ParameterInt</c>
    /// </summary>
    BasePointerRelative,

    /// <summary>
    /// <b>Only for stack!</b>
    /// <br/>
    /// <c>Memory.Stack.Count + CurrentInstruction.ParameterInt</c>
    /// </summary>
    StackRelative,
}

[Serializable]
[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
public class Instruction
{
    public AddressingMode AddressingMode;
    public Opcode opcode;
    DataItem parameter;

    public DataItem Parameter
    {
        get => parameter;
        set => parameter = value;
    }

    [Obsolete("Only for deserialization", true)]
    public Instruction()
    {
        this.opcode = Opcode.UNKNOWN;
        this.AddressingMode = AddressingMode.Absolute;
        this.parameter = DataItem.Null;
    }

    public Instruction(Opcode opcode)
    {
        this.opcode = opcode;
        this.AddressingMode = AddressingMode.Absolute;
        this.parameter = DataItem.Null;
    }
    public Instruction(Opcode opcode, DataItem parameter)
    {
        this.opcode = opcode;
        this.AddressingMode = AddressingMode.Absolute;
        this.parameter = parameter;
    }

    public Instruction(Opcode opcode, AddressingMode addressingMode)
    {
        this.opcode = opcode;
        this.AddressingMode = addressingMode;
        this.parameter = DataItem.Null;
    }
    public Instruction(Opcode opcode, AddressingMode addressingMode, DataItem parameter)
    {
        this.opcode = opcode;
        this.AddressingMode = addressingMode;
        this.parameter = parameter;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(opcode.ToString());

        if (opcode == Opcode.LOAD_VALUE ||
            opcode == Opcode.STORE_VALUE)
        {
            result.Append(' ');
            result.Append(AddressingMode.ToString());
        }

        if (!this.parameter.IsNull)
        { result.Append($" {{ {parameter} }}"); }

        return result.ToString();
    }
}
