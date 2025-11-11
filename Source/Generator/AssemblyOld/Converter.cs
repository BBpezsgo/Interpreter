using LanguageCore.Runtime;

namespace LanguageCore.Assembly.Generator;

[ExcludeFromCodeCoverage]
public static class ConverterForAsm
{
    public static string Convert(ReadOnlySpan<Instruction> instructions, DebugInformation? debugInformation, BitWidth bits)
    {
        AssemblyCode builder = new();

        List<int> codeReferences = new();
        for (int i = 0; i < instructions.Length; i++)
        {
            Instruction instruction = instructions[i];
            if (instruction.Opcode is Opcode.Jump)
            { codeReferences.Add(i + instruction.Operand1.Int); }
        }

        string registerBasePointer = bits switch
        {
            BitWidth._8 => "BP",
            BitWidth._16 => "BP",
            BitWidth._32 => "EBP",
            BitWidth._64 => "RBP",
            _ => throw new UnreachableException(),
        };

        for (int i = 0; i < instructions.Length; i++)
        {
            if (debugInformation is not null &&
                debugInformation.CodeComments.TryGetValue(i, out List<string>? comments))
            {
                foreach (string comment in comments)
                {
                    builder.CodeBuilder.AppendCommentLine(comment);
                }
            }

            if (codeReferences.Contains(i))
            {
                builder.CodeBuilder.AppendLabel($"_{i}");
            }

            Instruction instruction = instructions[i];

            builder.CodeBuilder.AppendText(' ', builder.CodeBuilder.Indent);

            switch (instruction.Opcode)
            {
                case Opcode.Jump:
                    builder.CodeBuilder.AppendTextLine($"JMP _{i + instruction.Operand1.Int}");
                    continue;
                case Opcode.Exit:
                    builder.CodeBuilder.AppendCommentLine("Finish");

                    switch (bits)
                    {
                        case BitWidth._32:
                        {
                            builder.CodeBuilder.AppendInstruction("MOV", "EBX", "0");
                            builder.CodeBuilder.AppendInstruction("MOV", "EAX", "1");
                            builder.CodeBuilder.AppendInstruction("SYSCALL");
                            break;
                        }
                        case BitWidth._64:
                        {
                            builder.CodeBuilder.AppendInstruction("MOV", "RDI", "0");
                            builder.CodeBuilder.AppendInstruction("MOV", "RAX", "60");
                            builder.CodeBuilder.AppendInstruction("SYSCALL");
                            break;
                        }
                        default: throw new NotImplementedException();
                    }

                    builder.CodeBuilder.AppendInstruction("HLT");
                    continue;
                default:
                {
                    int paramCount = instruction.Opcode.ParameterCount();

                    builder.CodeBuilder.AppendText(TextSectionBuilder.StringifyInstruction(instruction.Opcode));
                    if (paramCount >= 1)
                    {
                        builder.CodeBuilder.Builder.Append(' ');
                        builder.CodeBuilder.AppendText(instruction.Operand1.ToString());
                    }
                    if (paramCount >= 2)
                    {
                        InstructionOperand op2 = instruction.Operand2;

                        if (instruction.Operand1.BitWidth == BitWidth._64 &&
                            instruction.Operand2.BitWidth < BitWidth._64 &&
                            instruction.Operand2.Type == InstructionOperandType.Immediate32)
                        {
                            op2 = new InstructionOperand(op2.Value, InstructionOperandType.Immediate64);
                        }

                        builder.CodeBuilder.Builder.Append(',');
                        builder.CodeBuilder.AppendText(op2.ToString());
                    }

                    builder.CodeBuilder.AppendTextLine();
                    break;
                }
            }
        }

        builder.CodeBuilder.AppendCommentLine("Finish");

        switch (bits)
        {
            case BitWidth._32:
            {
                builder.CodeBuilder.AppendInstruction("MOV", "EBX", "0");
                builder.CodeBuilder.AppendInstruction("MOV", "EAX", "1");
                builder.CodeBuilder.AppendInstruction("SYSCALL");
                break;
            }
            case BitWidth._64:
            {
                builder.CodeBuilder.AppendInstruction("MOV", "RDI", "0");
                builder.CodeBuilder.AppendInstruction("MOV", "RAX", "60");
                builder.CodeBuilder.AppendInstruction("SYSCALL");
                break;
            }
            default: throw new NotImplementedException();
        }

        builder.CodeBuilder.AppendInstruction("HLT");
        return builder.Make(bits);
    }
}
