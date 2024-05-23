namespace LanguageCore.Runtime;

[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
public class PreparationInstruction
{
    public Opcode Opcode;
    public InstructionOperand Operand1;
    public InstructionOperand Operand2;

    public PreparationInstruction(
        Opcode opcode)
    {
        Opcode = opcode;
        Operand1 = default;
        Operand2 = default;
    }

    public PreparationInstruction(
        Opcode opcode,
        InstructionOperand operand1)
    {
        Opcode = opcode;
        Operand1 = operand1;
        Operand2 = default;
    }

    public PreparationInstruction(
        Opcode opcode,
        InstructionOperand operand1,
        InstructionOperand operand2)
    {
        Opcode = opcode;
        Operand1 = operand1;
        Operand2 = operand2;
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
