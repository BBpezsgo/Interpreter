namespace LanguageCore.Runtime;

[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
public class PreparationInstruction
{
    public AddressingMode AddressingMode;
    public Opcode Opcode;
    public DataItem Parameter;

    public PreparationInstruction(Opcode opcode, DataItem parameter)
    {
        Opcode = opcode;
        AddressingMode = AddressingMode.Absolute;
        Parameter = parameter;
    }

    public PreparationInstruction(Opcode opcode, AddressingMode addressingMode = AddressingMode.Absolute)
    {
        Opcode = opcode;
        AddressingMode = addressingMode;
        Parameter = DataItem.Null;
    }

    public PreparationInstruction(Opcode opcode, AddressingMode addressingMode, DataItem parameter)
    {
        Opcode = opcode;
        AddressingMode = addressingMode;
        Parameter = parameter;
    }

    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Opcode.ToString());

        if (Opcode == Opcode.LOAD_VALUE ||
            Opcode == Opcode.STORE_VALUE)
        {
            result.Append(' ');
            result.Append(AddressingMode.ToString());
        }

        if (!Parameter.IsNull)
        { result.Append($" {{ {Parameter} }}"); }

        return result.ToString();
    }
}
