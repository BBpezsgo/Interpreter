using System;

namespace LanguageCore.Brainfuck
{
    [Flags]
    public enum EasyBrainfuckCompilerFlags : int
    {
        None = 0b_0000,

        PrintCompiled = 0b_0001,
        PrintCompiledMinimized = 0b_0010,
        WriteToFile = 0b_0100,

        PrintFinal = 0b_1000,
    }
}
