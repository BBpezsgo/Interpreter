using System;
using System.IO;

namespace ProgrammingLanguage.Core
{
    using BBCode.Compiler;
    using Bytecode;

    /// <summary>
    /// A simpler form of <see cref="Interpreter"/><br/>
    /// Just call <see cref="Run(TheProgram.ArgumentParser.Settings)"/> and that's it
    /// </summary>
    class EasyInterpreter
    {
        public static void Run(TheProgram.ArgumentParser.Settings settings) => Run(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, settings.LogWarnings, !settings.ThrowErrors, settings.BasePath);

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
            bool LogWarnings = true,
            bool HandleErrors = true,
            string BasePath = ""
            )
        {
            if (LogDebug) Output.Output.Debug($"Run file \"{file.FullName}\" ...");
            string code = File.ReadAllText(file.FullName);
            Interpreter interpreter = new();

            interpreter.OnStdOut += (sender, data) => Output.Output.Write(data).Wait();
            interpreter.OnStdError += (sender, data) => Output.Output.WriteError(data).Wait();
            interpreter.OnExecuted += (sender, e) => { if (LogSystem) Output.Output.Log(e.ToString()); };

            interpreter.OnOutput += (sender, message, logType) =>
            {
                switch (logType)
                {
                    case Output.LogType.System:
                        if (!LogSystem) break;
                        Output.Output.Log(message);
                        break;
                    case Output.LogType.Normal:
                        Output.Output.Log(message);
                        break;
                    case Output.LogType.Warning:
                        if (!LogWarnings) break;
                        Output.Output.Warning(message);
                        break;
                    case Output.LogType.Error:
                        Output.Output.Error(message);
                        break;
                    case Output.LogType.Debug:
                        if (!LogDebug) break;
                        Output.Output.Debug(message);
                        break;
                }
            };

            interpreter.OnNeedInput += (sender) =>
            {
                var input = Console.ReadKey(true);
                sender.OnInput(input.KeyChar);
            };

#if DEBUG
            interpreter.OnDone += (sender, success) =>
            {
                if (sender.Details.Interpreter == null) return;

                Console.WriteLine($"");
                Console.WriteLine($" ===== HEAP ===== ");
                Console.WriteLine($"");

                sender.Details.Interpreter.Heap.DebugPrint();

                if (sender.Details.Interpreter.Stack.Count > 0 &&
                    sender.Details.Interpreter.Stack is DataStack dataStack)
                {
                    Console.WriteLine($"");
                    Console.WriteLine($" ===== STACK ===== ");
                    Console.WriteLine($"");

                    dataStack.DebugPrint();
                }
            };
#endif

            if (interpreter.Initialize())
            {
                interpreter.BasePath = BasePath;

                string dllsFolderPath = Path.Combine(file.Directory.FullName, BasePath.Replace('/', '\\'));

                if (Directory.Exists(dllsFolderPath))
                {
                    DirectoryInfo dllsFolder = new(dllsFolderPath);
                    if (LogDebug) Output.Output.Debug($"Load DLLs from \"{dllsFolder.FullName}\" ...");
                    FileInfo[] dlls = dllsFolder.GetFiles("*.dll");
                    foreach (var dll in dlls)
                    { interpreter.LoadDLL(dll.FullName); }
                }
                else
                {
                    Output.Output.Warning($"Folder \"{dllsFolderPath}\" doesn't exists!");
                }

                Instruction[] compiledCode = interpreter.CompileCode(file, compilerSettings, parserSettings, HandleErrors);
                if (compiledCode != null)
                { interpreter.ExecuteProgram(compiledCode, bytecodeInterpreterSettings); }
            }

            while (interpreter.IsExecutingCode)
            {
                interpreter.Update();
            }
        }
    }
}
