#define ENABLE_DEBUG

using System;
using System.IO;
using LanguageCore.Brainfuck;
using Win32;

namespace TheProgram
{
    public static class DevelopmentEntry
    {
#if !DEBUG || !ENABLE_DEBUG
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060")]
        public static bool Start(string[] args) => false;
#else
        [System.Diagnostics.CodeAnalysis.RequiresAssemblyFiles]
        public static bool Start(string[] args)
        {
            /*
            // {
            //     using InterpreterCompact interpreter = new();
            // 
            //     CompiledCode compiledCode = new();
            // 
            //     compiledCode.SetValue(1, 0);
            //     compiledCode.SetValue(2, 1);
            //     compiledCode.SetPointer(1);
            // 
            //     compiledCode.JumpStart16bit();
            // 
            //     // compiledCode.OUT_STRING(7, "Bruh");
            //     compiledCode.SetPointer(1);
            // 
            //     compiledCode.ClearCurrent16bit();
            // 
            //     compiledCode.JumpEnd();
            // 
            //     compiledCode.SetPointer(1);
            // 
            //     string code = compiledCode.ToString();
            //     code = BrainfuckCode.RemoveNoncodes(code);
            //     code = Minifier.Minify(code);
            // 
            //     interpreter.SetupUI();
            // 
            //     interpreter.LoadCode(code);
            //     interpreter.RunWithUI();
            //     interpreter.Draw();
            //     return true;
            // }
            // 
            // {
            //     using InterpreterCompact interpreter = new();
            //     bool @continue = true;
            //     ConsoleListener.KeyEvent += (e) =>
            //     {
            //         if (e.IsDown != 0)
            //         { @continue = true; }
            //     };
            // 
            //     int iterations = 256 * 254;
            // 
            //     CompiledCode compiledCode1 = new();
            //     compiledCode1.Append('>');
            //     compiledCode1.Add16bit();
            //     compiledCode1.Append('<');
            //     string code1 = compiledCode1.ToString();
            //     code1 = BrainfuckCode.RemoveNoncodes(code1);
            //     code1 = Minifier.Minify(code1);
            // 
            //     CompiledCode compiledCode2 = new();
            //     compiledCode2.Append('>');
            //     compiledCode2.Sub16bit();
            //     compiledCode2.Append('<');
            //     string code2 = compiledCode2.ToString();
            //     code2 = BrainfuckCode.RemoveNoncodes(code2);
            //     code2 = Minifier.Minify(code2);
            // 
            //     interpreter.SetupUI();
            // 
            //     void OnStep()
            //     {
            //         for (int i = 0; i < iterations; i++)
            //         {
            //             interpreter.LoadCode(code1);
            //             interpreter.Run();
            // 
            //             if ((i & 0b_0011_1111) == 0)
            //             {
            //                 interpreter.Draw();
            //             }
            //         }
            //         interpreter.Draw();
            // 
            //         for (int i = 0; i < iterations; i++)
            //         {
            //             interpreter.LoadCode(code2);
            //             interpreter.Run();
            // 
            //             if ((i & 0b_0011_1111) == 0)
            //             {
            //                 interpreter.Draw();
            //             }
            //         }
            //         interpreter.Draw();
            //     }
            // 
            //     while (true)
            //     {
            //         while (!@continue) ;
            //         @continue = false;
            // 
            //         OnStep();
            // 
            //         @continue = false;
            //     }
            // 
            //     return true;
            // }
            */

            // string path = Path.Combine(TestConstants.TestFilesPath, "..", "Examples", "calc.bbc");
            string path = Path.Combine(TestConstants.ExampleFilesPath, "calc.bbc");

            string[] generatedArgs =
            [
                // "--throw-errors",
                "--basepath \"../StandardLibrary/\"",
                // "--hide-debug",
                // "--dont-optimize",
                // "--console-gui",
                // "--print-instructions",
                "--brainfuck",
                // "--il",
                // "--asm",
                // "--no-nullcheck",
                // "--heap-size 0",
                "--no-pause",
                // "--show-progress",
                $"\"{path}\""
            ];

            string[] concatenatedArgs = new string[args.Length + generatedArgs.Length];
            args.CopyTo(concatenatedArgs, 0);
            generatedArgs.CopyTo(concatenatedArgs, args.Length);

            if (!ArgumentParser.Parse(out ProgramArguments settings, concatenatedArgs)) return true;

            try
            { Entry.Run(settings); }
            catch (Exception exception)
            { LanguageCore.Output.LogError($"Unhandled exception: {exception}"); }

            if (!settings.DoNotPause)
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }

            return true;
        }
#endif
    }
}
