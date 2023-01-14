using IngameCoding.BBCode.Compiler;
using IngameCoding.BBCode.Parser;
using IngameCoding.Bytecode;
using IngameCoding.Errors;
using IngameCoding.Terminal;

using System;
using System.IO;

using TheProgram;

namespace IngameCoding.Core
{
    /// <summary>
    /// A simpler form of <see cref="Interpreter"/><br/>
    /// Just call <see cref="Run(TheProgram.ArgumentParser.Settings)"/> and that's it
    /// </summary>
    class EasyInterpreter
    {
        public static void Run(TheProgram.ArgumentParser.Settings settings) => Run(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, !settings.ThrowErrors);
        public static void RunBinary(TheProgram.ArgumentParser.Settings settings) => RunBinary(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, !settings.ThrowErrors);

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

            codeInterpreter.OnStdOut += (sender, data) => Output.Output.Write(data).Wait();
            codeInterpreter.OnStdError += (sender, data) => Output.Output.WriteError(data).Wait();
            codeInterpreter.OnExecuted += (sender, e) => { if (LogSystem) Output.Output.Log(e.ToString()); };

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

            codeInterpreter.OnNeedInput += (sender) =>
            {
                var input = Console.ReadKey();
                sender.OnInput(input.KeyChar);
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

        public static void RunBinary(
            FileInfo file,
            BBCode.Parser.ParserSettings parserSettings,
            Compiler.CompilerSettings compilerSettings,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            bool LogDebug = true,
            bool LogSystem = true,
            bool HandleErrors = true
            )
        {
            if (LogDebug) Output.Output.Debug($"Run binary file '{file.FullName}'");
            var code = CompileIntoFile.ReadFile(file.FullName);
            var codeInterpreter = new Interpreter();

            codeInterpreter.OnStdOut += (sender, data) => Output.Output.Write(data).Wait();
            codeInterpreter.OnStdError += (sender, data) => Output.Output.WriteError(data).Wait();
            codeInterpreter.OnExecuted += (sender, e) => { if (LogSystem) Output.Output.Log(e.ToString()); };

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

            codeInterpreter.OnNeedInput += (sender) =>
            {
                var input = Console.ReadKey();
                sender.OnInput(input.KeyChar);
            };

            if (codeInterpreter.Initialize())
            {
                Instruction[] compiledCode = null;
                try
                {
                    compiledCode = codeInterpreter.ReadBinary(code, HandleErrors);
                }
                catch (IndexOutOfRangeException)
                {
                    if (!HandleErrors) throw;
                    Output.Output.Error($"Serialization Error: Ran out of bytes");
                    return;
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
