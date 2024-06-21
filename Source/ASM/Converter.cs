namespace LanguageCore.ASM.Generator;

using Runtime;

public static class ConverterForAsm
{
    public static string Convert(ReadOnlySpan<Instruction> instructions)
    {
        AssemblyCode builder = new();
        for (int i = 0; i < instructions.Length; i++)
        {
            Instruction instruction = instructions[i];
            builder.CodeBuilder.AppendInstruction(instruction);
        }
        builder.CodeBuilder.Call_stdcall("_ExitProcess@4");
        builder.CodeBuilder.AppendInstruction(OpCode.Halt);
        return builder.Make(true);
    }
}
