using IngameCoding.BBCode.Compiler;
using IngameCoding.Bytecode;
using IngameCoding.Errors;

using System;
using System.IO;

namespace IngameCoding.Core
{
    /// <summary>
    /// A simpler form of <see cref="Interpreter"/><br/>
    /// Just call <see cref="Run(TheProgram.ArgumentParser.Settings)"/> and that's it
    /// </summary>
    class EasyInterpreter
    {
        public static void Run(TheProgram.ArgumentParser.Settings settings) => Run(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, !settings.ThrowErrors, settings.BasePath);
        public static void RunCompiledFile(TheProgram.ArgumentParser.Settings settings) => RunCompiledFile(settings.File, settings.bytecodeInterpreterSettings, settings.CompileToFileType, settings.LogDebugs, settings.LogSystem, !settings.ThrowErrors);

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
            bool HandleErrors = true,
            string BasePath = ""
            )
        {
            if (LogDebug) Output.Output.Debug($"Run file \"{file.FullName}\" ...");
            var code = File.ReadAllText(file.FullName);
            var codeInterpreter = new Interpreter();

            codeInterpreter.OnStdOut += (sender, data) => Output.Output.Write(data).Wait();
            codeInterpreter.OnStdError += (sender, data) => Output.Output.WriteError(data).Wait();
            codeInterpreter.OnExecuted += (sender, e) => { if (LogSystem) Output.Output.Log(e.ToString()); };

            codeInterpreter.OnOutput += (sender, message, logType) =>
            {
                switch (logType)
                {
                    case Output.LogType.System:
                        if (LogSystem) Output.Output.Log(message);
                        break;
                    case Output.LogType.Normal:
                        Output.Output.Log(message);
                        break;
                    case Output.LogType.Warning:
                        Output.Output.Warning(message);
                        break;
                    case Output.LogType.Error:
                        Output.Output.Error(message);
                        break;
                    case Output.LogType.Debug:
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
                codeInterpreter.BasePath = BasePath;

                var dllsFolderPath = Path.Combine(file.Directory.FullName, BasePath.Replace('/', '\\'));

                if (Directory.Exists(dllsFolderPath))
                {
                    var dllsFolder = new DirectoryInfo(dllsFolderPath);
                    if (LogDebug) Output.Output.Debug($"Load DLLs from \"{dllsFolder.FullName}\" ...");
                    var dlls = dllsFolder.GetFiles("*.dll");
                    foreach (var dll in dlls)
                    { codeInterpreter.LoadDLL(dll.FullName); }
                }
                else
                {
                    Output.Output.Warning($"Folder \"{dllsFolderPath}\" doesn't exists!");
                }

                Instruction[] compiledCode = codeInterpreter.CompileCode(code, file, compilerSettings, parserSettings, HandleErrors);
                if (compiledCode != null)
                { codeInterpreter.RunCode(compiledCode, bytecodeInterpreterSettings); }
            }

            while (codeInterpreter.IsExecutingCode)
            {
                try
                {
                    codeInterpreter.Update();
                }
                catch (CompilerException error)
                {
                    Output.Output.Error($"CompilerException: {error.MessageAll}");
                    if (!HandleErrors) throw;
                }
                catch (RuntimeException error)
                {
                    Output.Output.Error($"RuntimeException: {error.MessageAll}");
                    if (!HandleErrors) throw;
                }
                catch (EndlessLoopException)
                {
                    Output.Output.Error($"Endless loop!!!");
                    if (!HandleErrors) throw;
                }
                catch (InternalException error)
                {
                    Output.Output.Error($"InternalException: {error.Message}");
                    if (!HandleErrors) throw;
                }
            }
        }

        public static void RunCompiledFile(
            FileInfo file,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            TheProgram.ArgumentParser.FileType fileType,
            bool LogDebug = true,
            bool LogSystem = true,
            bool HandleErrors = true
            )
        {
            if (LogDebug) Output.Output.Debug($"Run compiled file '{file.FullName}'");
            var codeInterpreter = new Interpreter();

            codeInterpreter.OnStdOut += (sender, data) => Output.Output.Write(data).Wait();
            codeInterpreter.OnStdError += (sender, data) => Output.Output.WriteError(data).Wait();
            codeInterpreter.OnExecuted += (sender, e) => { if (LogSystem) Output.Output.Log(e.ToString()); };

            codeInterpreter.OnOutput += (sender, message, logType) =>
            {
                switch (logType)
                {
                    case Output.LogType.System:
                        if (LogSystem) Output.Output.Log(message);
                        break;
                    case Output.LogType.Normal:
                        Output.Output.Log(message);
                        break;
                    case Output.LogType.Warning:
                        Output.Output.Warning(message);
                        break;
                    case Output.LogType.Error:
                        Output.Output.Error(message);
                        break;
                    case Output.LogType.Debug:
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
                    switch (fileType)
                    {
                        case TheProgram.ArgumentParser.FileType.Binary:
                            {
                                byte[] code = CompileIntoFile.ReadFile(file.FullName);
                                compiledCode = codeInterpreter.Read(code);
                                break;
                            }
                        case TheProgram.ArgumentParser.FileType.Readable:
                            {
                                string code = CompileIntoFile.ReadReadableFile(file.FullName);
                                compiledCode = codeInterpreter.Read(code);
                                break;
                            }
                        default:
                            Output.Output.Error("Bruh");
                            break;
                    }
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
                try
                {
                    codeInterpreter.Update();
                }
                catch (CompilerException error)
                {
                    Output.Output.Error($"CompilerException: {error.MessageAll}");
                    if (!HandleErrors) throw;
                }
                catch (RuntimeException error)
                {
                    Output.Output.Error($"RuntimeException: {error.MessageAll}");
                    if (!HandleErrors) throw;
                }
                catch (EndlessLoopException)
                {
                    Output.Output.Error($"Endless loop!!!");
                    if (!HandleErrors) throw;
                }
                catch (InternalException error)
                {
                    Output.Output.Error($"InternalException: {error.Message}");
                    if (!HandleErrors) throw;
                }
            }
        }
    }
}
