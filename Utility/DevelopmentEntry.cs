#if DEBUG

using System.IO;
using LanguageCore.ASM;
using LanguageCore.ASM.Generator;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;

namespace LanguageCore;


public static class DevelopmentEntry
{
    public static int Start(string[] args)
    {
        string path = Path.Combine(Program.ProjectPath, "TestFiles", $"02.{LanguageConstants.LanguageExtension}");

        /*
        {
            ImmutableArray<Runtime.Instruction> code = CodeGeneratorForMain.Generate(Compiler.Compiler.CompileFile(new FileInfo(path), null, CompilerSettings.Default, PreprocessorVariables.Normal), MainGeneratorSettings.Default).Code;
            string asm = ConverterForAsm.Convert(code.AsSpan());
            try
            {
                Assembler.Assemble(asm, "what.exe");
            }
            catch (NasmException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.GetArrows());
                Console.ResetColor();
                return;
            }
            Process process = Process.Start("what.exe");
            process.WaitForExit();
            Console.WriteLine(process.ExitCode);
            return;
        }
        */

        string[] generatedArgs =
        [
            "--throw-errors",
            "--basepath", "../StandardLibrary/",
            // "--verbose",
            // "--hide-info",
            // "--dont-optimize",
            // "--console-gui",
            // "--print", "i",
            // "--output", "bruh.bf",
            // "--brainfuck",
            "--format", "assembly",
            // "--no-nullcheck",
            // "--output", "./1.asm",
            // "--heap-size", "10000",
            // "--stack-size 40",
            // "--no-debug-info",
            // "--print-heap",
            // "--show-progress",
            // "https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/TestFiles/05.bbc",
            $"{path}"
        ];

        string[] concatenatedArgs = new string[args.Length + generatedArgs.Length];
        args.CopyTo(concatenatedArgs, 0);
        generatedArgs.CopyTo(concatenatedArgs, args.Length);

        return Entry.Run(concatenatedArgs);
    }
}

#endif
