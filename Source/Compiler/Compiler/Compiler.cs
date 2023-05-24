using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace IngameCoding.BBCode.Compiler
{
    using Bytecode;

    using Core;

    using DataUtilities.Serializer;

    using Errors;

    using Parser;
    using Parser.Statements;

    public class Compiler
    {
        public class CompilerResult : ISerializable<CompilerResult>
        {
            public Instruction[] compiledCode;

            public CompiledFunction[] compiledFunctions;
            public CompiledStruct[] compiledStructs;
            internal Dictionary<string, CompiledVariable> compiledVariables;

            public Dictionary<string, int> functionOffsets;
            public int clearGlobalVariablesInstruction;
            public int setGlobalVariablesInstruction;
            internal DebugInfo[] debugInfo;

            public bool GetFunctionOffset(FunctionDefinition functionCallStatement, out int functionOffset)
            {
                if (functionOffsets.TryGetValue(functionCallStatement.Identifier.Content, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.FullName, out functionOffset))
                {
                    return true;
                }
                functionOffset = -1;
                return false;
            }

            internal void WriteToConsole()
            {
                Console.WriteLine("\n\r === INSTRUCTIONS ===\n\r");
                int indent = 0;

                for (int i = 0; i < this.compiledCode.Length; i++)
                {
                    if (this.clearGlobalVariablesInstruction == i)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("ClearGlobalVariables:");
                        Console.ResetColor();
                    }
                    if (this.setGlobalVariablesInstruction == i)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("SetGlobalVariables:");
                        Console.ResetColor();
                    }

                    Instruction instruction = this.compiledCode[i];
                    if (instruction.opcode == Opcode.COMMENT)
                    {
                        if (!instruction.ParameterString.EndsWith("{ }") && instruction.ParameterString.EndsWith("}"))
                        {
                            indent--;
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"{"  ".Repeat(indent)}{instruction.ParameterString}");
                        Console.ResetColor();

                        if (!instruction.ParameterString.EndsWith("{ }") && instruction.ParameterString.EndsWith("{"))
                        {
                            indent++;
                        }

                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"{"  ".Repeat(indent)} {instruction.opcode}");
                    Console.Write($" ");

                    if (instruction.Parameter is int || instruction.Parameter is float)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.Parameter}");
                        Console.Write($" ");
                    }
                    else if (instruction.Parameter is bool)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write($"{instruction.Parameter}");
                        Console.Write($" ");
                    }
                    else if (instruction.Parameter is string parameterString)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"\"{parameterString.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n")}\"");
                        Console.Write($" ");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{instruction.Parameter}");
                        Console.Write($" ");
                    }

                    if (!string.IsNullOrEmpty(instruction.tag))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"{instruction.tag}");
                    }

                    Console.Write("\n\r");

                    Console.ResetColor();
                }

                Console.WriteLine("\n\r === ===\n\r");
            }

            public void CheckFilePaths(System.Action<string> NotSetCallback)
            {
                foreach (var func in compiledFunctions)
                {
                    if (string.IsNullOrEmpty(func.FilePath))
                    { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {func} is null"); }
                    else
                    { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {func} : {func}"); }
                }
                foreach (var var_ in compiledVariables)
                {
                    var @var = var_.Value;
                    if (string.IsNullOrEmpty(@var.FilePath))
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {@var} is null"); }
                    else
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {@var} : {@var.FilePath}"); }
                }
                foreach (var @struct in compiledStructs)
                {
                    if (string.IsNullOrEmpty(@struct.FilePath))
                    { NotSetCallback?.Invoke($"StructDefinition.FilePath {@struct} is null"); }
                    else
                    { NotSetCallback?.Invoke($"StructDefinition.FilePath {@struct} : {@struct.FilePath}"); }
                }
            }

            void ISerializable<CompilerResult>.Serialize(Serializer serializer)
            {
                serializer.Serialize(setGlobalVariablesInstruction);
                serializer.Serialize(clearGlobalVariablesInstruction);
                serializer.Serialize(compiledCode);
                serializer.Serialize(functionOffsets);
            }

            void ISerializable<CompilerResult>.Deserialize(Deserializer deserializer)
            {
                this.setGlobalVariablesInstruction = deserializer.DeserializeInt32();
                this.clearGlobalVariablesInstruction = deserializer.DeserializeInt32();
                this.compiledCode = deserializer.DeserializeObjectArray<Instruction>();
                this.functionOffsets = deserializer.DeserializeDictionary<string, int>();
            }
        }

        public struct CompilerSettings
        {
            public bool GenerateComments;
            public byte RemoveUnusedFunctionsMaxIterations;
            public bool PrintInstructions;
            public bool DontOptimize;
            public bool GenerateDebugInstructions;

            public static CompilerSettings Default => new()
            {
                GenerateComments = true,
                RemoveUnusedFunctionsMaxIterations = 4,
                PrintInstructions = false,
                DontOptimize = false,
                GenerateDebugInstructions = true,
            };
        }

        public enum CompileLevel
        {
            Minimal,
            Exported,
            All,
        }

        static void CompileFile(
            List<string> alreadyCompiledCodes,
            UsingDefinition @using,
            FileInfo file,
            ParserSettings parserSettings,
            Dictionary<string, FunctionDefinition> Functions,
            Dictionary<string, StructDefinition> Structs,
            Dictionary<string, ClassDefinition> Classes,
            List<Statement_HashInfo> Hashes,
            List<Error> errors,
            List<Warning> warnings,
            Action<string, Output.LogType> printCallback,
            string basePath,
            CompileLevel level)
        {
            string content = null;
            string path = null;

            if (@using.IsUrl)
            {
                @using.Path[0].Analysis.CompilerReached = true;

                if (!Uri.TryCreate(@using.Path[0].Content, UriKind.Absolute, out var uri))
                { throw new SyntaxException($"Invalid uri \"{@using.Path[0].Content}\"", @using.Path[0], file.FullName); }

                path = uri.ToString();

                @using.CompiledUri = path;

                if (alreadyCompiledCodes.Contains(path))
                {
                    printCallback?.Invoke($" Skip file \"{path}\" ...", Output.LogType.Debug);
                    return;
                }
                alreadyCompiledCodes.Add(path);

                printCallback?.Invoke($" Download file \"{path}\" ...", Output.LogType.Debug);
                var started = DateTime.Now;
                System.Net.Http.HttpClient httpClient = new();
                System.Threading.Tasks.Task<string> req;
                try
                {
                    req = httpClient.GetStringAsync(uri);
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    throw new Exception($"HTTP GET Error", ex);
                }
                req.Wait();
                @using.DownloadTime = (DateTime.Now - started).TotalMilliseconds;

                printCallback?.Invoke($" File \"{path}\" downloaded", Output.LogType.Debug);

                content = req.Result;
            }
            else
            {
                if (string.IsNullOrEmpty(basePath))
                {
                    FileInfo[] configFiles = file.Directory.GetFiles("config.json");
                    if (configFiles.Length == 1)
                    {
                        System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configFiles[0].FullName));
                        if (document.RootElement.TryGetProperty("base", out var v))
                        {
                            string b = v.GetString();
                            if (b != null) basePath = b;
                        }
                    }
                }

                for (int i = 0; i < @using.Path.Length; i++) @using.Path[i].Analysis.CompilerReached = true;

                string filename = @using.PathString.Replace("/", "\\");
                if (!filename.EndsWith("." + FileExtensions.Code)) filename += "." + FileExtensions.Code;

                List<string> searchForThese = new();
                try
                { searchForThese.Add(Path.GetFullPath(basePath.Replace("/", "\\") + filename, file.Directory.FullName)); }
                catch (System.Exception) { }
                try
                { searchForThese.Add(Path.GetFullPath(filename, file.Directory.FullName)); }
                catch (System.Exception) { }

                bool found = false;
                for (int i = 0; i < searchForThese.Count; i++)
                {
                    path = searchForThese[i];
                    if (!File.Exists(path))
                    { continue; }
                    else
                    { found = true; break; }
                }

                @using.CompiledUri = path;

                if (!found)
                { errors.Add(new Error($"File \"{path}\" not found", new Position(@using.Path))); return; }

                if (alreadyCompiledCodes.Contains(path))
                {
                    printCallback?.Invoke($" Skip file \"{path}\" ...", Output.LogType.Debug);
                    return;
                }
                alreadyCompiledCodes.Add(path);

                content = File.ReadAllText(path);
            }

            printCallback?.Invoke($" Parse file \"{path}\" ...", Output.LogType.Debug);
            ParserResult parserResult2 = Parser.Parse(content, warnings, (msg, lv) => printCallback?.Invoke($"  {msg}", lv));

            parserResult2.SetFile(path);

            if (parserSettings.PrintInfo)
            { parserResult2.WriteToConsole($"PARSER INFO FOR '{@using.PathString}'"); }

            foreach (var func in parserResult2.Functions)
            {
                var id = func.ID();

                if (Functions.ContainsKey(id))
                { errors.Add(new Error($"Function '{id}' already exists", func.Identifier)); continue; }

                Functions.Add(id, func);
            }

            foreach (var @struct in parserResult2.Structs)
            {
                if (Classes.ContainsKey(@struct.Key) || Structs.ContainsKey(@struct.Key))
                {
                    errors.Add(new Error($"Type '{@struct.Value.FullName}' already exists", @struct.Value.Name));
                }
                else
                {
                    Structs.Add(@struct.Key, @struct.Value);
                }
            }

            foreach (var @class in parserResult2.Classes)
            {
                if (Classes.ContainsKey(@class.Key) || Structs.ContainsKey(@class.Key))
                {
                    errors.Add(new Error($"Type '{@class.Value.FullName}' already exists", @class.Value.Name));
                }
                else
                {
                    Classes.Add(@class.Key, @class.Value);
                }
            }

            foreach (var hash in parserResult2.Hashes)
            {
                Hashes.Add(hash);
            }

            foreach (UsingDefinition using_ in parserResult2.Usings)
            { CompileFile(alreadyCompiledCodes, using_, file, parserSettings, Functions, Structs, Classes, Hashes, errors, warnings, printCallback, basePath, level); }
        }

        /// <summary>
        /// Compiles the source code into a list of instructions
        /// </summary>
        /// <param name="code">
        /// The source code
        /// </param>
        /// <param name="file">
        /// The source code file
        /// </param>
        /// <param name="result">
        /// The codeGenerator result
        /// </param>
        /// <param name="warnings">
        /// A list that this can fill with warnings
        /// </param>
        /// <param name="errors">
        /// A list that this can fill with errors
        /// </param>
        /// <param name="printCallback">
        /// Optional: Print callback
        /// </param>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="Exception"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="System.Exception"/>
        public static CompilerResult CompileCode(
            ParserResult parserResult,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            FileInfo file,
            List<Warning> warnings,
            List<Error> errors,
            CompilerSettings settings,
            ParserSettings parserSettings,
            Action<string, Output.LogType> printCallback = null,
            string basePath = "",
            CompileLevel level = CompileLevel.Minimal)
        {
            Dictionary<string, FunctionDefinition> Functions = new();
            Dictionary<string, StructDefinition> Structs = new();
            Dictionary<string, ClassDefinition> Classes = new();
            List<Statement_HashInfo> Hashes = new();
            List<string> AlreadyCompiledCodes = new() { file.FullName };

            if (parserResult.Usings.Count > 0)
            { printCallback?.Invoke("Parse usings ...", Output.LogType.Debug); }

            foreach (UsingDefinition usingItem in parserResult.Usings)
            {
                List<Error> compileErrors = new();
                CompileFile(AlreadyCompiledCodes, usingItem, file, parserSettings, Functions, Structs, Classes, Hashes, compileErrors, warnings, printCallback, basePath, level);
                if (compileErrors.Count > 0)
                { throw new System.Exception($"Failed to compile file {usingItem.PathString}", compileErrors[0].ToException()); }
            }

            if (parserSettings.PrintInfo)
            { parserResult.WriteToConsole(); }

            foreach (var func in parserResult.Functions)
            {
                var id = func.ID();

                if (Functions.ContainsKey(id))
                { errors.Add(new Error($"Function '{id}' already exists", func.Identifier)); continue; }

                Functions.Add(id, func);
            }
            foreach (var @struct in parserResult.Structs)
            {
                Structs.Add(@struct.Key, @struct.Value);
            }
            foreach (var @class in parserResult.Classes)
            {
                Classes.Add(@class.Key, @class.Value);
            }

            DateTime codeGenerationStarted = DateTime.Now;
            printCallback?.Invoke("Generating code ...", Output.LogType.Debug);

            CodeGenerator codeGenerator = new()
            { warnings = warnings, errors = errors, hints = new List<Hint>(), informations = new List<Information>() };
            var codeGeneratorResult = codeGenerator.GenerateCode(Functions, Structs, Classes, Hashes.ToArray(), parserResult.GlobalVariables, builtinFunctions, settings, printCallback, level);

            printCallback?.Invoke($"Code generated in {(DateTime.Now - codeGenerationStarted).TotalMilliseconds} ms", Output.LogType.Debug);

            Dictionary<string, int> functionOffsets = new();
            foreach (var function in codeGenerator.compiledFunctions) functionOffsets.Add(function.Key, function.InstructionOffset);

            return new CompilerResult()
            {
                compiledCode = codeGeneratorResult.compiledCode,
                debugInfo = codeGeneratorResult.DebugInfo,

                compiledStructs = codeGeneratorResult.compiledStructs.ToArray(),
                compiledFunctions = codeGeneratorResult.compiledFunctions.ToArray(),
                compiledVariables = codeGenerator.compiledVariables.ToDictionary(),

                clearGlobalVariablesInstruction = codeGeneratorResult.clearGlobalVariablesInstruction,
                setGlobalVariablesInstruction = codeGeneratorResult.setGlobalVariablesInstruction,
                functionOffsets = functionOffsets,
            };
        }
    }
}