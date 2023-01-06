using IngameCoding.BBCode.Compiler;
using IngameCoding.Bytecode;
using IngameCoding.Errors;
using IngameCoding.Terminal;

using System;
using System.IO;

namespace IngameCoding.Core
{
    /// <summary>
    /// A simpler form of <see cref="Interpreter"/><br/>
    /// Just call <see cref="Run(string, bool)"/> and that's it
    /// </summary>
    class EasyInterpreter
    {
        public static void Run(TheProgram.ArgumentParser.Settings settings) => Run(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, !settings.ThrowErrors);

        /// <summary>
        /// Compiles and interprets source code
        /// </summary>
        /// <param name="path">
        /// The path to the source code file
        /// </param>
        /// <param name="HandleErrors">
        /// Throw or print exceptions?
        /// </param>
        public static void Run(
            FileInfo file,
            BBCode.Parser.ParserSettings parserSettings,
            Compiler.CompilerSettings compilerSettings,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            bool LogDebug = true,
            bool LogSystem = true,
            bool HandleErrors = true
            )
        {
            if (LogDebug) Output.Output.Debug($"Run file '{file.FullName}'");
            var code = File.ReadAllText(file.FullName);
            var codeInterpreter = new Interpreter();

            codeInterpreter.OnExecuted += (sender, e) =>
            {
                if (LogSystem) Output.Output.Log(e.ToString());
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
                Instruction[] compiledCode;

                if (file.Extension.ToLower() == ".bcc")
                {
                    var tokens = BCCode.Tokenizer.Parse(code);

                    var parser = new BCCode.Parser();
                    var (statements, labels) = parser.Parse(tokens);

                    compiledCode = BCCode.Parser.GenerateCode(statements, labels);
                }
                else
                {
                    compiledCode = codeInterpreter.CompileCode(code, file, compilerSettings, parserSettings, HandleErrors);
                }

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
