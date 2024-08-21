namespace LanguageCore.ASM.Generator;

using Runtime;

[ExcludeFromCodeCoverage]
public static class ConverterForAsm
{
    public static string Convert(ReadOnlySpan<Instruction> instructions)
    {
        AssemblyCode builder = new();

        List<int> codeReferences = new();
        for (int i = 0; i < instructions.Length; i++)
        {
            Instruction instruction = instructions[i];
            if (instruction.Opcode is Opcode.Jump)
            { codeReferences.Add(i + instruction.Operand1.Int); }
        }

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

                    builder.CodeBuilder.AppendInstruction(OpCode.Move, "RDI", "0");
                    builder.CodeBuilder.AppendInstruction(OpCode.Move, "RAX", "60");
                    builder.CodeBuilder.AppendInstruction(OpCode.SystemCall);

                    // builder.CodeBuilder.Call_stdcall("_ExitProcess@4");

                    builder.CodeBuilder.AppendInstruction(OpCode.Halt);
                    continue;
                case Opcode.Pop8:
                    builder.CodeBuilder.AppendTextLine($"ADD EBP,1");
                    continue;
                case Opcode.Pop16:
                    builder.CodeBuilder.AppendTextLine($"ADD EBP,2");
                    continue;
                case Opcode.Pop32:
                    builder.CodeBuilder.AppendTextLine($"ADD EBP,4");
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
                        builder.CodeBuilder.Builder.Append(',');
                        builder.CodeBuilder.AppendText(instruction.Operand2.ToString());
                    }

                    builder.CodeBuilder.AppendTextLine();
                    break;
                }
            }
        }

        builder.CodeBuilder.AppendCommentLine("Finish");

        builder.CodeBuilder.AppendInstruction(OpCode.Move, "RDI", "0");
        builder.CodeBuilder.AppendInstruction(OpCode.Move, "RAX", "60");
        builder.CodeBuilder.AppendInstruction(OpCode.SystemCall);

        // builder.CodeBuilder.Call_stdcall("_ExitProcess@4");

        builder.CodeBuilder.AppendInstruction(OpCode.Halt);
        return builder.Make(false);
    }
}
