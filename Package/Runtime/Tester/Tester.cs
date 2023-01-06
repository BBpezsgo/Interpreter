using IngameCoding.BBCode;
using IngameCoding.BBCode.Compiler;
using IngameCoding.Bytecode;
using IngameCoding.Core;
using IngameCoding.Errors;
using IngameCoding.Terminal;

using System;
using System.Collections.Generic;
using System.IO;

namespace IngameCoding.Tester
{
    class Tester
    {
        public static void RunTestFile(TheProgram.ArgumentParser.Settings settings) => RunTestFile(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, settings.HandleErrors);

        static void PrintWarnings(Warning[] warnings)
        {
            foreach (var warning in warnings) Output.Output.Warning(warning);
        }

        public static void RunTestFile(
            FileInfo file,
            BBCode.Parser.ParserSettings parserSettings,
            BBCode.Compiler.Compiler.CompilerSettings compilerSettings,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            bool LogDebug = true,
            bool LogSystem = true,
            bool HandleErrors = true
            )
        {
            List<Warning> warnings = new();
            Parser.ParserResult parserResult;
            try
            {
                parserResult = Parser.Parser.Parse(System.IO.File.ReadAllText(file.FullName), warnings);
                PrintWarnings(warnings.ToArray());
            }
            catch (Errors.Exception error)
            {
                if (!HandleErrors) throw;
                PrintWarnings(warnings.ToArray());
                Output.Output.Error(error);
                return;
            }

            warnings.Clear();
            Compiler.CompilerResult compilerResult;
            try
            {
                compilerResult = Compiler.Compile(parserResult, warnings, file.Directory, file.FullName);
                PrintWarnings(warnings.ToArray());
            }
            catch (CompilerException error)
            {
                if (!HandleErrors) throw;
                Output.Output.Error(error);
                PrintWarnings(warnings.ToArray());
                return;
            }

            Output.Output.Debug("Start testing ...");

            foreach (var item in compilerResult.Tests)
            {
                if (item.Disabled) continue;

                string testFilePath = item.File;
                var testFile = new FileInfo(testFilePath);

                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.Write("\n === ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(item.Name);
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.Write(" === \n");
                Console.ResetColor();
                Console.WriteLine();

                RunFile(testFile, parserSettings, compilerSettings, bytecodeInterpreterSettings, LogDebug, LogSystem, HandleErrors);
            }
        }

        static void RunFile(
            FileInfo file,
            BBCode.Parser.ParserSettings parserSettings,
            BBCode.Compiler.Compiler.CompilerSettings compilerSettings,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            bool LogDebug = true,
            bool LogSystem = true,
            bool HandleErrors = true
            )
        {
            if (LogDebug) Output.Output.Debug($"Run test with file '{file.FullName}'");
            var code = File.ReadAllText(file.FullName);
            var codeInterpreter = new Interpreter();

            codeInterpreter.OnExecuted += (sender, e) =>
            {
                Console.WriteLine();
                if (e.ExitCode == -1)
                {
                    Console.Write($"Time: {e.ElapsedTime}, Exit code: ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(e.ExitCode);
                    Console.ResetColor();
                }
                else
                {
                    Console.Write($"Time: {e.ElapsedTime}, Exit code: ");
                    Console.Write(e.ExitCode);
                }
                Console.WriteLine();
            };

            codeInterpreter.OnOutput += (sender, message, logType) =>
            {
                switch (logType)
                {
                    case TerminalInterpreter.LogType.System:
                        if (LogSystem) Output.Output.Log(message);
                        break;
                    case TerminalInterpreter.LogType.Normal:
                        Output.Output.Log(message);
                        break;
                    case TerminalInterpreter.LogType.Warning:
                        Output.Output.Warning(message);
                        break;
                    case TerminalInterpreter.LogType.Error:
                        Output.Output.Error(message);
                        break;
                    case TerminalInterpreter.LogType.Debug:
                        if (LogDebug) Output.Output.Debug(message);
                        break;
                }
            };

            codeInterpreter.OnNeedInput += (sender, message) =>
            {
                Console.Write(message);
                var input = Console.ReadLine();
                sender.OnInput(input);
            };

            if (codeInterpreter.Initialize())
            {
                Instruction[] compiledCode = codeInterpreter.CompileCode(code, file, compilerSettings, parserSettings, HandleErrors);

                if (compiledCode != null)
                { codeInterpreter.RunCode(compiledCode, bytecodeInterpreterSettings); }
            }

            while (codeInterpreter.IsExecutingCode)
            {
                if (HandleErrors)
                {
                    try
                    {
                        codeInterpreter.Update();
                    }
                    catch (CompilerException error)
                    {
                        Output.Output.Error($"CompilerException: {error.MessageAll}");
                    }
                    catch (RuntimeException error)
                    {
                        Output.Output.Error($"RuntimeException: {error.MessageAll}");
                    }
                    catch (EndlessLoopException)
                    {
                        Output.Output.Error($"Endless loop!!!");
                    }
                    catch (InternalException error)
                    {
                        Output.Output.Error($"InternalException: {error.Message}");
                    }
                }
                else
                {
                    codeInterpreter.Update();
                }
            }
        }
    }
}
