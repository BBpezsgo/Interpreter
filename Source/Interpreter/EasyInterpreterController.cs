using System;
using System.IO;

namespace LanguageCore.Runtime
{
    using BBCode.Generator;

    class EasyInterpreter
    {
        public static void Run(TheProgram.ArgumentParser.Settings settings)
            => Run(
                settings.File!,
                settings.compilerSettings,
                settings.bytecodeInterpreterSettings,
                settings.LogDebugs,
                settings.LogSystem,
                settings.LogWarnings,
                settings.LogInfo,
                !settings.ThrowErrors);

        public static void Run(
            FileInfo file,
            Compiler.CompilerSettings compilerSettings,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            bool logDebug = true,
            bool logSystem = true,
            bool logWarnings = true,
            bool logInfo = true,
            bool handleErrors = true)
        {
            if (logDebug) Output.LogDebug($"Run file \"{file.FullName}\" ...");
            string code = File.ReadAllText(file.FullName);
            Interpreter interpreter = new();

            interpreter.OnStdOut += (sender, data) => Output.Write(data);
            interpreter.OnStdError += (sender, data) => Output.WriteError(data);

            void PrintOutput(string message, LogType logType)
            {
                switch (logType)
                {
                    case LogType.System:
                        if (!logSystem) break;
                        Output.Log(message);
                        break;
                    case LogType.Normal:
                        if (!logInfo) break;
                        Output.Log(message);
                        break;
                    case LogType.Warning:
                        if (!logWarnings) break;
                        Output.LogWarning(message);
                        break;
                    case LogType.Error:
                        Output.LogError(message);
                        break;
                    case LogType.Debug:
                        if (!logDebug) break;
                        Output.LogDebug(message);
                        break;
                }
            }

            interpreter.OnOutput += (_, message, logType) => PrintOutput(message, logType);

            interpreter.OnNeedInput += (sender) =>
            {
                ConsoleKeyInfo input = Console.ReadKey(true);
                sender.OnInput(input.KeyChar);
            };

#if DEBUG
            interpreter.OnExecuted += (sender, e) =>
            {
                if (logSystem) Output.Log(e.ToString());

                if (sender.BytecodeInterpreter == null) return;

                Console.WriteLine();
                Console.WriteLine($" ===== HEAP ===== ");
                Console.WriteLine();

                sender.BytecodeInterpreter.Memory.Heap.DebugPrint();

                if (sender.BytecodeInterpreter.Memory.Stack.Count > 0 &&
                    sender.BytecodeInterpreter.Memory.Stack is DataStack dataStack)
                {
                    Console.WriteLine();
                    Console.WriteLine($" ===== STACK ===== ");
                    Console.WriteLine();

                    dataStack.DebugPrint();
                }
            };
#endif

            if (interpreter.Initialize())
            {
                string dllsFolderPath = Path.Combine(file.Directory!.FullName, compilerSettings.BasePath?.Replace('/', '\\') ?? string.Empty);

#if AOT
                Output.Log($"Skipping loading DLL-s because the compiler compiled in AOT mode");
#else
                if (Directory.Exists(dllsFolderPath))
                {
                    DirectoryInfo dllsFolder = new(dllsFolderPath);
                    if (logDebug) Output.LogDebug($"Load DLLs from \"{dllsFolder.FullName}\" ...");
                    FileInfo[] dlls = dllsFolder.GetFiles("*.dll");
                    foreach (FileInfo dll in dlls)
                    { interpreter.LoadDLL(dll.FullName); }
                }
                else
                {
                    Output.LogWarning($"Folder \"{dllsFolderPath}\" doesn't exists!");
                }
#endif
                Compiler.CompilerResult compiled;
                BBCodeGeneratorResult generatedCode;

                if (handleErrors)
                {
                    try
                    {
                        compiled = LanguageCore.Compiler.Compiler.Compile(Parser.Parser.ParseFile(file.FullName), interpreter.GenerateExternalFunctions(), file, compilerSettings.BasePath, PrintOutput);
                        generatedCode = CodeGeneratorForMain.Generate(compiled, compilerSettings, PrintOutput);
                    }
                    catch (Exception ex)
                    {
                        PrintOutput(ex.ToString(), LogType.Error);
                        return;
                    }
                }
                else
                {
                    compiled = LanguageCore.Compiler.Compiler.Compile(Parser.Parser.ParseFile(file.FullName), interpreter.GenerateExternalFunctions(), file, compilerSettings.BasePath, PrintOutput);
                    generatedCode = CodeGeneratorForMain.Generate(compiled, compilerSettings, PrintOutput);
                }

                interpreter.CompilerResult = generatedCode;
                interpreter.ExecuteProgram(generatedCode.Code, bytecodeInterpreterSettings);
            }

            while (interpreter.IsExecutingCode)
            {
                interpreter.Update();
            }
        }
    }
}
