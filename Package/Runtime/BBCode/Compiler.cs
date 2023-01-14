using System;
using System.Collections.Generic;
using System.IO;

namespace IngameCoding.BBCode.Compiler
{
    using Bytecode;

    using Core;

    using Errors;

    using IngameCoding.Serialization;

    using Parser;
    using Parser.Statements;

    using Terminal;

    public class Compiler
    {
        public class CompilerResult : Serialization.ISerializable<CompilerResult>
        {
            public Instruction[] compiledCode;

            public Dictionary<string, CompiledFunction> compiledFunctions;
            public Dictionary<string, CompiledStruct> compiledStructs;
            internal Dictionary<string, CompiledVariable> compiledGlobalVariables;
            internal Dictionary<string, CompiledVariable> compiledVariables;

            public Dictionary<string, int> functionOffsets;
            public int clearGlobalVariablesInstruction;
            public int setGlobalVariablesInstruction;

            bool GetCompiledVariable(string variableName, out CompiledVariable compiledVariable, out bool isGlobal)
            {
                isGlobal = false;
                if (compiledVariables.TryGetValue(variableName, out compiledVariable))
                {
                    return true;
                }
                else if (compiledGlobalVariables.TryGetValue(variableName, out compiledVariable))
                {
                    isGlobal = true;
                    return true;
                }
                return false;
            }

            bool GetCompiledStruct(string structName, out CompiledStruct compiledStruct)
            { return compiledStructs.TryGetValue(structName, out compiledStruct); }

            public bool GetFunctionOffset(Statement_FunctionCall functionCallStatement, out int functionOffset)
            {
                if (functionOffsets.TryGetValue(functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                functionOffset = -1;
                return false;
            }
            public bool GetFunctionOffset(Statement_MethodCall methodCallStatement, out int functionOffset)
            {
                if (GetCompiledVariable(methodCallStatement.VariableName, out CompiledVariable compiledVariable, out _))
                {
                    bool IsStructMethodCall = true;
                    if (!GetCompiledStruct(compiledVariable.structName, out _))
                    { IsStructMethodCall = false; }
                    else
                    {
                        if (!compiledStructs[compiledVariable.structName].CompiledMethods.ContainsKey(methodCallStatement.FunctionName))
                        { IsStructMethodCall = false; }
                    }

                    if (!IsStructMethodCall)
                    {
                        if (functionOffsets.TryGetValue(methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                        else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                        else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                        else if (functionOffsets.TryGetValue(methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                    }
                }

                functionOffset = -1;
                return false;
            }

            public bool GetFunctionOffset(FunctionDefinition functionCallStatement, out int functionOffset)
            {
                if (functionOffsets.TryGetValue(functionCallStatement.Name.text, out functionOffset))
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
                        if (!instruction.parameter.ToString().EndsWith("{ }") && instruction.parameter.ToString().EndsWith("}"))
                        {
                            indent--;
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"{"  ".Repeat(indent)}{instruction.parameter}");
                        Console.ResetColor();

                        if (!instruction.parameter.ToString().EndsWith("{ }") && instruction.parameter.ToString().EndsWith("{"))
                        {
                            indent++;
                        }

                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"{"  ".Repeat(indent)} {instruction.opcode}");
                    Console.Write($" ");

                    if (instruction.parameter is int || instruction.parameter is float)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.parameter}");
                        Console.Write($" ");
                    }
                    else if (instruction.parameter is bool)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write($"{instruction.parameter}");
                        Console.Write($" ");
                    }
                    else if (instruction.parameter is string)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"\"{instruction.parameter}\"");
                        Console.Write($" ");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{instruction.parameter}");
                        Console.Write($" ");
                    }

                    if (!string.IsNullOrEmpty(instruction.additionParameter))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"\"{instruction.additionParameter}\"");
                        Console.Write($" ");
                    }

                    if (instruction.additionParameter2 != -1)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.additionParameter2}");
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
                foreach (var func_ in compiledFunctions)
                {
                    var func = func_.Value;
                    if (string.IsNullOrEmpty(func.FilePath))
                    { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {func} is null"); }
                    else
                    { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {func} : {func}"); }
                }
                foreach (var var_ in compiledGlobalVariables)
                {
                    var @var = var_.Value;
                    if (string.IsNullOrEmpty(@var.Declaration.FilePath))
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {@var} is null"); }
                    else
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {@var} : {@var.Declaration.FilePath}"); }
                }
                foreach (var var_ in compiledVariables)
                {
                    var @var = var_.Value;
                    if (string.IsNullOrEmpty(@var.Declaration.FilePath))
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {@var} is null"); }
                    else
                    { NotSetCallback?.Invoke($"GlobalVariable.FilePath {@var} : {@var.Declaration.FilePath}"); }
                }
                foreach (var struct_ in compiledStructs)
                {
                    var @struct = struct_.Value;
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
                serializer.SerializeObjectArray(compiledCode);
                serializer.Serialize(functionOffsets, false);
            }

            void ISerializable<CompilerResult>.Deserialize(Deserializer deserializer)
            {
                this.setGlobalVariablesInstruction = deserializer.DeserializeInt32();
                this.clearGlobalVariablesInstruction = deserializer.DeserializeInt32();
                this.compiledCode = deserializer.DeserializeObjectArray<Instruction>();
                this.functionOffsets = deserializer.DeserializeDictionary<string, int>(false, false);
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

        static void CompileFile(
            List<string> alreadyCompiledCodes,
            Token[] usingPath,
            FileInfo file,
            string usingItem,
            ParserSettings parserSettings,
            Dictionary<string, FunctionDefinition> Functions,
            Dictionary<string, StructDefinition> Structs,
            List<Statement_HashInfo> Hashes,
            List<Error> errors,
            List<Warning> warnings,
            Action<string, TerminalInterpreter.LogType> printCallback)
        {
            var filePath = file.Directory.FullName + "\\" + usingItem + "." + FileExtensions.Code;
            if (File.Exists(filePath))
            {
                if (alreadyCompiledCodes.Contains(filePath))
                {
                    printCallback?.Invoke($" Skip file \"{filePath}\" ...", TerminalInterpreter.LogType.Debug);
                    return;
                }

                alreadyCompiledCodes.Add(filePath);

                printCallback?.Invoke($" Parse file \"{filePath}\" ...", TerminalInterpreter.LogType.Debug);
                ParserResult parserResult2 = Parser.Parse(File.ReadAllText(file.Directory.FullName + "\\" + usingItem + "." + Core.FileExtensions.Code), warnings,
                    (msg, lv) => { printCallback?.Invoke($"  {msg}", lv); });

                parserResult2.SetFile(filePath);

                if (parserSettings.PrintInfo)
                { parserResult2.WriteToConsole($"PARSER INFO FOR '{usingItem}'"); }

                foreach (var func in parserResult2.Functions)
                {
                    var id = func.ID();

                    if (Functions.ContainsKey(id))
                    { errors.Add(new Error($" Function '{id}' already exists", func.Name)); continue; }

                    Functions.Add(id, func);
                }

                foreach (var @struct in parserResult2.Structs)
                {
                    if (Structs.ContainsKey(@struct.Key))
                    {
                        errors.Add(new Error($" Struct '{@struct.Value.FullName}' already exists", @struct.Value.Name));
                    }
                    else
                    {
                        Structs.Add(@struct.Key, @struct.Value);
                    }
                }

                foreach (var hash in parserResult2.Hashes)
                {
                    Hashes.Add(hash);
                }

                foreach (UsingDefinition usingItem_ in parserResult2.Usings)
                { CompileFile(alreadyCompiledCodes, usingItem_.Path, file, usingItem_.PathString, parserSettings, Functions, Structs, Hashes, errors, warnings, printCallback); }
            }
            else
            { errors.Add(new Error($" File \"{filePath}\" not found", new Position(usingPath))); }
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
            Dictionary<string, Func<IStruct>> builtinStructs,
            FileInfo file,
            List<Warning> warnings,
            List<Error> errors,
            CompilerSettings settings,
            ParserSettings parserSettings,
            Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            Dictionary<string, FunctionDefinition> Functions = new();
            Dictionary<string, StructDefinition> Structs = new();
            List<Statement_HashInfo> Hashes = new();
            List<string> AlreadyCompiledCodes = new() { file.FullName };

            if (parserResult.Usings.Count > 0)
            { printCallback?.Invoke("Parse usings ...", TerminalInterpreter.LogType.Debug); }

            foreach (UsingDefinition usingItem in parserResult.Usings)
            { CompileFile(AlreadyCompiledCodes, usingItem.Path, file, usingItem.PathString, parserSettings, Functions, Structs, Hashes, errors, warnings, printCallback); }

            if (parserSettings.PrintInfo)
            { parserResult.WriteToConsole(); }

            foreach (var func in parserResult.Functions)
            {
                var id = func.ID();

                if (Functions.ContainsKey(id))
                { errors.Add(new Error($"Function '{id}' already exists", func.Name)); continue; }

                Functions.Add(id, func);
            }
            foreach (var @struct in parserResult.Structs)
            {
                Structs.Add(@struct.Key, @struct.Value);
            }

            DateTime codeGenerationStarted = DateTime.Now;
            printCallback?.Invoke("Generating code ...", TerminalInterpreter.LogType.Debug);

            CodeGenerator codeGenerator = new()
            { warnings = warnings, errors = errors, hints = new List<Hint>(), informations = new List<Information>() };
            var codeGeneratorResult = codeGenerator.GenerateCode(Functions, Structs, Hashes.ToArray(), parserResult.GlobalVariables, builtinFunctions, builtinStructs, settings, printCallback);

            printCallback?.Invoke($"Code generated in {(DateTime.Now - codeGenerationStarted).TotalMilliseconds} ms", TerminalInterpreter.LogType.Debug);

            return new CompilerResult()
            {
                compiledCode = codeGeneratorResult.compiledCode,

                compiledStructs = codeGeneratorResult.compiledStructs,
                compiledFunctions = codeGeneratorResult.compiledFunctions,
                compiledGlobalVariables = codeGenerator.compiledGlobalVariables,
                compiledVariables = codeGenerator.compiledVariables,

                clearGlobalVariablesInstruction = codeGeneratorResult.clearGlobalVariablesInstruction,
                setGlobalVariablesInstruction = codeGeneratorResult.setGlobalVariablesInstruction,
                functionOffsets = codeGenerator.functionOffsets,
            };
        }
    }
}