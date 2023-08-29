using ProgrammingLanguage.Brainfuck;
using ProgrammingLanguage.Brainfuck.Compiler;
using ProgrammingLanguage.Core;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

#nullable enable

namespace TheProgram.Brainfuck
{
    internal static class ProgramUtils
    {
        public static void SpeedTest(string code, int iterations)
        {
            ProgrammingLanguage.Brainfuck.Interpreter interpreter = new(code, _ => { });

            int line = Console.GetCursorPosition().Top;
            Console.ResetColor();
            Stopwatch sw = new();
            for (int i = 0; i < iterations; i++)
            {
                Console.SetCursorPosition(0, line);
                Console.Write($"Running iteration {i} / {iterations} ...         ");
                interpreter.Reset();

                sw.Start();
                interpreter.Run();
                sw.Stop();
            }

            Console.SetCursorPosition(0, line);
            Console.WriteLine($"Execution time: {sw.ElapsedMilliseconds} ms                 ");
        }

        [Flags]
        public enum CompileOptions : int
        {
            None = 0b_0000,

            PrintCompiled = 0b_0001,
            PrintCompiledMinimized = 0b_0010,
            WriteToFile = 0b_0100,

            PrintFinal = 0b_1000,
        }

        public static CodeGenerator.Result? CompilePlus(FileInfo file, CompileOptions options)
                => CompilePlus(file, (int)options);
        public static CodeGenerator.Result? CompilePlus(FileInfo file, int options)
        {
            CodeGenerator.Settings compilerSettings = CodeGenerator.Settings.Default;

            string code = File.ReadAllText(file.FullName);

            compilerSettings.ClearGlobalVariablesBeforeExit = true;

            bool throwErrors = true;

            CodeGenerator.Result compilerResult;

            try
            {
                compilerResult = EasyCompiler.Compile(file, ProgrammingLanguage.BBCode.Compiler.Compiler.CompilerSettings.Default, compilerSettings, PrintMessage).CodeGeneratorResult;
                Console.WriteLine($"Optimalized {compilerResult.Optimalizations} statements");
            }
            catch (ProgrammingLanguage.Errors.Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.ToString());

                string? arrows = GetArrows(exception.Position, code);
                if (arrows != null)
                { Console.WriteLine(arrows); }

                Console.ResetColor();

                if (throwErrors) throw;
                else return null;
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.ToString());

                Console.ResetColor();

                if (throwErrors) throw;
                else return null;
            }

            if ((options & (int)CompileOptions.PrintCompiled) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === COMPILED ===");
                PrintCode(Simplifier.Simplify(compilerResult.Code));
                Console.WriteLine();
                Console.ResetColor();
            }

            compilerResult.Code = Minifier.Minify(compilerResult.Code);

            if ((options & (int)CompileOptions.PrintCompiledMinimized) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === MINIFIED ===");
                PrintCode(Simplifier.Simplify(compilerResult.Code));
                Console.WriteLine();
                Console.ResetColor();
            }

            compilerResult.Code = Minifier.Minify(Utils.RemoveNoncodes(compilerResult.Code));

            if ((options & (int)CompileOptions.PrintFinal) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === FINAL ===");
                PrintCode(compilerResult.Code);
                Console.WriteLine();
                Console.ResetColor();
            }

            if ((options & (int)CompileOptions.WriteToFile) != 0)
            {
                string compiledFilePath = Path.Combine(Path.GetDirectoryName(file.FullName) ?? TestConstants.TestFilesPath, Path.GetFileNameWithoutExtension(file.FullName) + ".bf");
                File.WriteAllText(compiledFilePath, compilerResult.Code);
            }

            return compilerResult;
        }

        static void PrintMessage(string message, ProgrammingLanguage.Output.LogType level)
        {
            switch (level)
            {
                case ProgrammingLanguage.Output.LogType.System:
                    ProgrammingLanguage.Output.Output.Log(message);
                    break;
                case ProgrammingLanguage.Output.LogType.Normal:
                    ProgrammingLanguage.Output.Output.Log(message);
                    break;
                case ProgrammingLanguage.Output.LogType.Warning:
                    ProgrammingLanguage.Output.Output.Warning(message);
                    break;
                case ProgrammingLanguage.Output.LogType.Error:
                    ProgrammingLanguage.Output.Output.Error(message);
                    break;
                case ProgrammingLanguage.Output.LogType.Debug:
                    ProgrammingLanguage.Output.Output.Debug(message);
                    break;
                default:
                    ProgrammingLanguage.Output.Output.Log(message);
                    break;
            }
        }

        public static string? Compile(string code, CompileOptions options)
            => Compile(code, (int)options);
        public static string Compile(string code, int options)
        {
            string compiled = Minifier.Minify(Utils.RemoveNoncodes(code));

            if ((options & (int)CompileOptions.PrintFinal) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === FINAL ===");
                PrintCode(compiled);
                Console.WriteLine();
                Console.ResetColor();
            }

            return compiled;
        }

        public static string? CompileFile(string file, CompileOptions options)
            => CompileFile(file, (int)options);
        public static string? CompileFile(string file, int options)
        {
            if (!File.Exists(file))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File \"{file}\" does not exists");
                Console.ResetColor();
                return null;
            }

            string extension = Path.GetExtension(file)[1..];
            if (extension == "bfpp")
            {
                return CompilePlus(new FileInfo(file), options)?.Code;
            }

            if (extension == "bf")
            {
                return Compile(File.ReadAllText(file), options);
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Unknown extension \"{extension}\"");
            Console.ResetColor();
            return null;
        }

        public static string? GetArrows(Position position, string text)
        {
            if (position.AbsolutePosition == new Range<int>(0, 0)) return null;
            if (position.Start.Line != position.End.Line)
            { return null; }
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (position.Start.Line - 1 >= lines.Length)
            { return null; }
            string line = lines[position.Start.Line - 1];

            string result = "";

            result += line.Replace('\t', ' ');
            result += "\r\n";
            result += new string(' ', Math.Max(0, position.Start.Character - 1));
            result += new string('^', Math.Max(1, position.End.Character - position.Start.Character));
            return result;
        }

        public static void PrintCode(string code)
        {
            bool expectNumber = false;
            for (int i = 0; i < code.Length; i++)
            {
                switch (code[i])
                {
                    case '>':
                    case '<':
                        if (Console.ForegroundColor != ConsoleColor.Red) Console.ForegroundColor = ConsoleColor.Red;
                        expectNumber = true;
                        break;
                    case '+':
                    case '-':
                        if (Console.ForegroundColor != ConsoleColor.Blue) Console.ForegroundColor = ConsoleColor.Blue;
                        expectNumber = true;
                        break;
                    case '[':
                    case ']':
                        if (Console.ForegroundColor != ConsoleColor.Green) Console.ForegroundColor = ConsoleColor.Green;
                        expectNumber = false;
                        break;
                    case '.':
                    case ',':
                        if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta;
                        expectNumber = false;
                        break;
                    default:
                        if (expectNumber && (new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }).Contains(code[i]))
                        {
                            if (Console.ForegroundColor != ConsoleColor.Yellow) Console.ForegroundColor = ConsoleColor.Yellow;
                        }
                        else if (Utils.CodeCharacters.Contains(code[i]))
                        {
                            expectNumber = false;
                            if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta;
                        }
                        else
                        {
                            expectNumber = false;
                            if (Console.ForegroundColor != ConsoleColor.DarkGray) Console.ForegroundColor = ConsoleColor.DarkGray;
                        }
                        break;
                }
                Console.Write(code[i]);
            }
        }

        public static void PrintCodeChar(char code)
        {
            switch (code)
            {
                case '>':
                case '<':
                    if (Console.ForegroundColor != ConsoleColor.Red) Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case '+':
                case '-':
                    if (Console.ForegroundColor != ConsoleColor.Blue) Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case '[':
                case ']':
                    if (Console.ForegroundColor != ConsoleColor.Green) Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case '.':
                case ',':
                    if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                default:
                    if (Utils.CodeCharacters.Contains(code))
                    { if (Console.ForegroundColor != ConsoleColor.Magenta) Console.ForegroundColor = ConsoleColor.Magenta; }
                    else
                    { if (Console.ForegroundColor != ConsoleColor.DarkGray) Console.ForegroundColor = ConsoleColor.DarkGray; }
                    break;
            }
            Console.Write(code);
        }
    }
}
