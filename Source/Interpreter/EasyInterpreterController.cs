﻿using System;
using System.IO;

namespace LanguageCore.Runtime
{
    using BBCode.Compiler;

    /// <summary>
    /// A simpler form of <see cref="Interpreter"/><br/>
    /// Just call <see cref="Run(TheProgram.ArgumentParser.Settings)"/> and that's it
    /// </summary>
    class EasyInterpreter
    {
        public static void Run(TheProgram.ArgumentParser.Settings settings) => Run(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, settings.LogWarnings, settings.LogInfo, !settings.ThrowErrors, settings.BasePath);

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
            LanguageCore.Parser.ParserSettings parserSettings,
            Compiler.CompilerSettings compilerSettings,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            bool LogDebug = true,
            bool LogSystem = true,
            bool LogWarnings = true,
            bool LogInfo = true,
            bool HandleErrors = true,
            string basePath = ""
            )
        {
            if (LogDebug) Output.Debug($"Run file \"{file.FullName}\" ...");
            string code = File.ReadAllText(file.FullName);
            Interpreter interpreter = new();

            interpreter.OnStdOut += (sender, data) => Output.Write(data).Wait();
            interpreter.OnStdError += (sender, data) => Output.WriteError(data).Wait();

            void PrintOutput(string message, LogType logType)
            {
                switch (logType)
                {
                    case LogType.System:
                        if (!LogSystem) break;
                        Output.Log(message);
                        break;
                    case LogType.Normal:
                        if (!LogInfo) break;
                        Output.Log(message);
                        break;
                    case LogType.Warning:
                        if (!LogWarnings) break;
                        Output.Warning(message);
                        break;
                    case LogType.Error:
                        Output.Error(message);
                        break;
                    case LogType.Debug:
                        if (!LogDebug) break;
                        Output.Debug(message);
                        break;
                }
            }

            interpreter.OnOutput += (_, message, logType) => PrintOutput(message, logType);

            interpreter.OnNeedInput += (sender) =>
            {
                var input = Console.ReadKey(true);
                sender.OnInput(input.KeyChar);
            };

#if DEBUG
            interpreter.OnExecuted += (sender, e) =>
            {
                if (LogSystem) Output.Log(e.ToString());

                if (sender.BytecodeInterpreter == null) return;

                Console.WriteLine($"");
                Console.WriteLine($" ===== HEAP ===== ");
                Console.WriteLine($"");

                sender.BytecodeInterpreter.Memory.Heap.DebugPrint();

                if (sender.BytecodeInterpreter.Memory.Stack.Count > 0 &&
                    sender.BytecodeInterpreter.Memory.Stack is DataStack dataStack)
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
                string dllsFolderPath = Path.Combine(file.Directory.FullName, basePath.Replace('/', '\\'));

                if (Directory.Exists(dllsFolderPath))
                {
                    DirectoryInfo dllsFolder = new(dllsFolderPath);
                    if (LogDebug) Output.Debug($"Load DLLs from \"{dllsFolder.FullName}\" ...");
                    FileInfo[] dlls = dllsFolder.GetFiles("*.dll");
                    foreach (var dll in dlls)
                    { interpreter.LoadDLL(dll.FullName); }
                }
                else
                {
                    Output.Warning($"Folder \"{dllsFolderPath}\" doesn't exists!");
                }

                CodeGenerator.Result? compiledCode = LanguageCore.BBCode.EasyCompiler.Compile(
                    file,
                    interpreter.GenerateExternalFunctions(),
                    Tokenizing.TokenizerSettings.Default,
                    parserSettings,
                    compilerSettings,
                    HandleErrors,
                    PrintOutput,
                    basePath
                    );

                if (compiledCode.HasValue)
                {
                    interpreter.CompilerResult = compiledCode.Value;
                    interpreter.ExecuteProgram(compiledCode.Value.Code, bytecodeInterpreterSettings);
                }
            }

            while (interpreter.IsExecutingCode)
            {
                interpreter.Update();
            }
        }
    }
}