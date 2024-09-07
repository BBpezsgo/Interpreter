namespace LanguageCore.ASM.Generator;

using Runtime;

[ExcludeFromCodeCoverage]
public static class ConverterForAsm
{
    public static string Convert(ReadOnlySpan<Instruction> instructions, BitWidth bits)
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
                            builder.CodeBuilder.AppendInstruction(OpCode.Move, "EBX", "0");
                            builder.CodeBuilder.AppendInstruction(OpCode.Move, "EAX", "1");
                            builder.CodeBuilder.AppendInstruction(OpCode.SystemCall);
                            break;
                        }
                        case BitWidth._64:
                        {
                            builder.CodeBuilder.AppendInstruction(OpCode.Move, "RDI", "0");
                            builder.CodeBuilder.AppendInstruction(OpCode.Move, "RAX", "60");
                            builder.CodeBuilder.AppendInstruction(OpCode.SystemCall);
                            break;
                        }
                        default: throw new NotImplementedException();
                    }

                    builder.CodeBuilder.AppendInstruction(OpCode.Halt);
                    continue;
                case Opcode.Pop8:
                    builder.CodeBuilder.AppendTextLine($"ADD {registerBasePointer}, 1");
                    continue;
                case Opcode.Pop16:
                    builder.CodeBuilder.AppendTextLine($"ADD {registerBasePointer}, 2");
                    continue;
                case Opcode.Pop32:
                    builder.CodeBuilder.AppendTextLine($"ADD {registerBasePointer}, 4");
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
                builder.CodeBuilder.AppendInstruction(OpCode.Move, "EBX", "0");
                builder.CodeBuilder.AppendInstruction(OpCode.Move, "EAX", "1");
                builder.CodeBuilder.AppendInstruction(OpCode.SystemCall);
                break;
            }
            case BitWidth._64:
            {
                builder.CodeBuilder.AppendInstruction(OpCode.Move, "RDI", "0");
                builder.CodeBuilder.AppendInstruction(OpCode.Move, "RAX", "60");
                builder.CodeBuilder.AppendInstruction(OpCode.SystemCall);
                break;
            }
            default: throw new NotImplementedException();
        }

        builder.CodeBuilder.AppendInstruction(OpCode.Halt);
        return builder.Make(bits);
    }
}
