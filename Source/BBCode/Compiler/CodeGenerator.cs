using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode.Compiler
{
    using ProgrammingLanguage.BBCode.Analysis;
    using ProgrammingLanguage.BBCode.Parser;
    using ProgrammingLanguage.BBCode.Parser.Statement;
    using ProgrammingLanguage.Bytecode;
    using ProgrammingLanguage.Core;
    using ProgrammingLanguage.Errors;

    using System.ComponentModel.DataAnnotations;

    readonly struct CleanupItem
    {
        /// <summary>
        /// The actual data size on the stack
        /// </summary>
        internal readonly int Size;
        /// <summary>
        /// The element count
        /// </summary>
        internal readonly int Count;

        public CleanupItem(int size, int count)
        {
            Size = size;
            Count = count;
        }
    }

    public class DebugInfo
    {
        internal Position Position;
        internal int InstructionStart;
        internal int InstructionEnd;
    }

    public class CodeGenerator : CodeGeneratorBase
    {
        #region Fields

        Stack<CleanupItem> CleanupStack;

        Dictionary<string, BuiltinFunction> BuiltinFunctions;
        Dictionary<string, int> BuiltinFunctionCache;
        IInContext<CompiledClass> CurrentContext;

        List<int> ReturnInstructions;
        List<List<int>> BreakInstructions;

        List<Instruction> GeneratedCode;

        List<UndefinedFunctionOffset> UndefinedFunctionOffsets;
        List<UndefinedOperatorFunctionOffset> UndefinedOperatorFunctionOffsets;
        List<UndefinedGeneralFunctionOffset> UndefinedGeneralFunctionOffsets;

        bool OptimizeCode;
        bool AddCommentsToCode = true;
        readonly bool TrimUnreachableCode = true;
        bool GenerateDebugInstructions = true;

        List<Information> Informations;
        public List<Hint> Hints;
        readonly List<DebugInfo> GeneratedDebugInfo = new();

        List<KeyValuePair<string, CompiledVariable>> compiledVariables;
        List<CompiledParameter> parameters;

        #endregion

        public CodeGenerator() : base() { }

        #region Helper Functions

        protected override bool GetLocalSymbolType(string symbolName, out CompiledType type)
        {
            if (GetVariable(symbolName, out CompiledVariable variable))
            {
                type = variable.Type;
                return true;
            }

            if (GetParameter(symbolName, out CompiledParameter parameter))
            {
                type = parameter.Type;
                return true;
            }

            type = null;
            return false;
        }

        bool GetVariable(string variableName, out CompiledVariable compiledVariable)
            => compiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, out CompiledParameter parameter)
            => parameters.TryGetValue(parameterName, out parameter);

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(TypeInstance type, string tag = "")
        {
            if (Constants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out Type builtinType))
            {
                AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(builtinType), tag);
                return 1;
            }

            ITypeDefinition instanceType = GetCustomType(type);

            if (instanceType is CompiledStruct @struct)
            {
                int size = 0;
                foreach (FieldDefinition field in @struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (instanceType is CompiledClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            if (instanceType is CompiledEnum @enum)
            {
                if (@enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{@enum.Identifier.Content}\" initial value: enum has no members", @enum.Identifier, @enum.FilePath); }

                AddInstruction(Opcode.PUSH_VALUE, @enum.Members[0].Value, tag);
                return 1;
            }

            if (instanceType is FunctionType function)
            {
                AddInstruction(Opcode.PUSH_VALUE, function.Function.InstructionOffset, tag);
                return 1;
            }

            throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", type.Identifier, CurrentFile);
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, string tag = "")
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (FieldDefinition field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
            return 1;
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, Action<int> afterValue, string tag = "")
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (FieldDefinition field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                    afterValue?.Invoke(size);
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, Utils.NULL_POINTER, $"(pointer) {tag}");
                afterValue?.Invoke(0);
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
            afterValue?.Invoke(0);
            return 1;
        }

        int ParameterSizeSum()
        {
            int sum = 0;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Type.IsClass) sum++;
                else sum += parameters[i].Type.Size;
            }
            return sum;
        }
        int ParameterSizeSum(int beforeThis)
        {
            int sum = 0;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Index < beforeThis) continue;
                if (parameters[i].Type.IsClass) sum++;
                else sum += parameters[i].Type.Size;
            }
            return sum;
        }

        #endregion

        #region AddInstruction()

        void AddInstruction(Instruction instruction)
        {
            if (instruction.opcode == Opcode.COMMENT && !AddCommentsToCode) return;
            if (instruction.opcode == Opcode.DEBUG_SET_TAG && !GenerateDebugInstructions) return;

            GeneratedCode.Add(instruction);
        }
        void AddInstruction(Opcode opcode) => AddInstruction(new Instruction(opcode));
        void AddInstruction(Opcode opcode, DataItem param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, string tag = null) => AddInstruction(new Instruction(opcode) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, new DataItem(param0)) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, float param0, string tag = null) => AddInstruction(new Instruction(opcode, new DataItem(param0)) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, byte param0, string tag = null) => AddInstruction(new Instruction(opcode, new DataItem(param0)) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, char param0, string tag = null) => AddInstruction(new Instruction(opcode, new DataItem(param0)) { tag = tag ?? string.Empty });

        void AddInstruction(Opcode opcode, AddressingMode addressingMode) => AddInstruction(new Instruction(opcode, addressingMode));
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, DataItem param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, new DataItem(param0)) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, float param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, new DataItem(param0)) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, byte param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, new DataItem(param0)) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, char param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, new DataItem(param0)) { tag = tag ?? string.Empty });

        void AddComment(string comment) => AddInstruction(Opcode.COMMENT, comment);
        #endregion

        bool GetCompiledField(Field field, out CompiledField compiledField)
        {
            compiledField = null;

            CompiledType type = FindStatementType(field.PrevStatement);
            if (type is null) return false;
            if (!type.IsClass) return false;
            var @class = type.Class;
            for (int i = 0; i < @class.Fields.Length; i++)
            {
                if (@class.Fields[i].Identifier.Content != field.FieldName.Content) continue;

                compiledField = @class.Fields[i];
                return true;
            }
            return false;
        }

        #region GenerateCodeForStatement

        void GenerateCodeForStatement(VariableDeclaretion newVariable)
        {
            newVariable.VariableName.AnalysedType = TokenAnalysedType.VariableName;

            if (!GetVariable(newVariable.VariableName.Content, out _))
            { throw new CompilerException($"Variable \"{newVariable.VariableName.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", newVariable.VariableName, CurrentFile); }

            if (newVariable.InitialValue == null) return;

            AddComment($"New Variable \'{newVariable.VariableName.Content}\' {{");

            GenerateCodeForValueSetter(newVariable, newVariable.InitialValue);

            AddComment("}");
        }
        void GenerateCodeForStatement(KeywordCall keywordCall)
        {
            AddComment($"Call Keyword {keywordCall.FunctionName} {{");

            if (keywordCall.FunctionName == "return")
            {
                if (keywordCall.Parameters.Length > 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"return\": requied {0} or {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

                if (keywordCall.Parameters.Length == 1)
                {
                    AddComment(" Param 0:");

                    StatementWithValue returnValue = keywordCall.Parameters[0];
                    CompiledType returnValueType = FindStatementType(returnValue);

                    GenerateCodeForStatement(returnValue);
                    int offset = GetReturnValueAddress();

                    for (int i = 0; i < returnValueType.SizeOnStack; i++)
                    { AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset - i); }
                }

                keywordCall.Identifier = keywordCall.Identifier.Statement("return", "void", new string[] { "p" }, new string[] { "any" });

                AddComment(" .:");

                ReturnInstructions.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                AddComment("}");

                return;
            }

            if (keywordCall.FunctionName == "throw")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"throw\": requied {1} passed {keywordCall.Parameters}", keywordCall, CurrentFile); }

                AddComment(" Param 0:");

                StatementWithValue throwValue = keywordCall.Parameters[0];
                // CompiledType throwValueType = FindStatementType(throwValue);

                GenerateCodeForStatement(throwValue);
                AddInstruction(Opcode.THROW);

                keywordCall.Identifier = keywordCall.Identifier.Statement("throw", "void", new string[] { "errorMessage" }, new string[] { "any" });

                return;
            }

            if (keywordCall.FunctionName == "break")
            {
                if (BreakInstructions.Count == 0)
                { throw new CompilerException($"The keyword \"break\" does not avaiable in the current context", keywordCall.Identifier, CurrentFile); }

                BreakInstructions.Last().Add(GeneratedCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                AddComment("}");

                return;
            }

            if (keywordCall.FunctionName == "sizeof")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": requied {1} passed {keywordCall.Parameters.Length}", keywordCall.TotalPosition(), CurrentFile); }

                StatementWithValue param0 = keywordCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                AddInstruction(Opcode.PUSH_VALUE, param0Type.SizeOnHeap, $"sizeof({param0Type.Name})");

                keywordCall.Identifier = keywordCall.Identifier.BuiltinFunction("sizeof", "int", new string[] { "p" }, new string[] { "any" });

                return;
            }

            if (keywordCall.FunctionName == "delete")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"delete\": requied {1} passed {keywordCall.Parameters.Length}", keywordCall.TotalPosition(), CurrentFile); }

                CompiledType paramType = FindStatementType(keywordCall.Parameters[0]);

                if (paramType == Type.INT)
                {
                    keywordCall.Identifier = keywordCall.Identifier.BuiltinFunction("delete", "void", new string[] { "address" }, new string[] { "int" });

                    GenerateCodeForStatement(keywordCall.Parameters[0]);
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    return;
                }

                if (paramType.IsClass)
                {
                    keywordCall.Identifier = keywordCall.Identifier.BuiltinFunction("delete", "void", new string[] { "object" }, new string[] { "any" });

                    if (!GetDestructor(paramType.Class, out var destructor))
                    {
                        return;
                    }

                    if (!destructor.CanUse(CurrentFile))
                    {
                        Errors.Add(new Error($"Destructor for type '{paramType.Class.Name.Content}' function cannot be called due to its protection level", keywordCall.Identifier, CurrentFile));
                        AddComment("}");
                        return;
                    }

                    AddComment(" Param0:");
                    GenerateCodeForStatement(keywordCall.Parameters[0]);
                    AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");

                    AddComment(" .:");

                    if (destructor.InstructionOffset == -1)
                    { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(GeneratedCode.Count, keywordCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

                    AddInstruction(Opcode.CALL, destructor.InstructionOffset - GeneratedCode.Count);

                    AddComment(" Clear Param:");

                    AddInstruction(Opcode.POP_VALUE);

                    AddComment("}");

                    return;
                }

                Warnings.Add(new Warning($"The 'delete' keyword-function is only working on type class or int so I skip this shit", keywordCall.Parameters[0].TotalPosition(), CurrentFile));
                return;
            }

            if (keywordCall.FunctionName == "clone")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"clone\": requied {1} passed {0}", keywordCall.TotalPosition(), CurrentFile); }

                var paramType = FindStatementType(keywordCall.Parameters[0]);

                if (!paramType.IsClass)
                {
                    Warnings.Add(new Warning($"The 'clone' function is only working on type class so I skip this shit", keywordCall.Parameters[0].TotalPosition(), CurrentFile));
                    return;
                }

                keywordCall.Identifier = keywordCall.Identifier.BuiltinFunction("clone", "void", new string[] { "object" }, new string[] { "any" });

                if (!GetCloner(paramType.Class, out var cloner))
                { throw new CompilerException($"Cloner for type \"{paramType.Class.Name}\" not found. Check if you defined a general function with name \"clone\" in class \"{paramType.Class.Name}\"", keywordCall.Identifier, CurrentFile); }

                if (!cloner.CanUse(CurrentFile))
                {
                    Errors.Add(new Error($"Cloner for type \"{paramType.Class.Name.Content}\" function could not be called due to its protection level.", keywordCall.Identifier, CurrentFile));
                    AddComment("}");
                    return;
                }

                int returnValueSize = 0;
                if (cloner.ReturnSomething)
                {
                    returnValueSize = GenerateInitialValue(cloner.Type, "returnvalue");
                }

                AddComment($" Param {0}:");
                GenerateCodeForStatement(keywordCall.Parameters[0]);
                AddInstruction(Opcode.DEBUG_SET_TAG, $"param.{cloner.Parameters[0].Identifier}");

                AddComment(" .:");

                if (cloner.InstructionOffset == -1)
                { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(GeneratedCode.Count, keywordCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

                AddInstruction(Opcode.CALL, cloner.InstructionOffset - GeneratedCode.Count);


                AddComment(" Clear Params:");

                AddInstruction(Opcode.POP_VALUE);

                if (cloner.ReturnSomething && !keywordCall.SaveValue)
                {
                    AddComment(" Clear Return Value:");
                    for (int i = 0; i < returnValueSize; i++)
                    { AddInstruction(Opcode.POP_VALUE); }
                }

                AddComment("}");

                return;
            }

            if (keywordCall.FunctionName == "out")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"out\": requied {1} passed {0}", keywordCall.TotalPosition(), CurrentFile); }

                var outType = FindStatementType(keywordCall.Parameters[0]);

                if (!GetOutputWriter(outType, out var function))
                { throw new CompilerException($"No function found with attribute \"{"StandardOutput"}\" that satisfies keyword-call {keywordCall.ReadableID(FindStatementType)}", keywordCall, CurrentFile); }

                if (function.IsBuiltin)
                {
                    if (!BuiltinFunctions.TryGetValue(function.BuiltinName, out var builtinFunction))
                    {
                        Errors.Add(new Error($"Builtin function \"{function.BuiltinName}\" not found", keywordCall.Identifier, CurrentFile));
                        AddComment("}");
                        return;
                    }

                    AddComment(" Function Name:");
                    if (BuiltinFunctionCache.TryGetValue(function.BuiltinName, out int cacheAddress))
                    {
                        if (function.BuiltinName.Length == 0)
                        { throw new CompilerException($"Builtin function with length of zero", (FunctionDefinition.Attribute)function.Attributes.Get("Builtin"), function.FilePath); }

                        AddComment($" Param {0}:");
                        GenerateCodeForStatement(keywordCall.Parameters[0]);
                        AddInstruction(Opcode.PUSH_VALUE, function.BuiltinName.Length, "ID Length");

                        AddComment($" Load Function Name String Pointer (Cache):");
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress);

                        AddComment(" .:");
                        AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                        if (function.ReturnSomething)
                        {
                            if (!keywordCall.SaveValue)
                            {
                                AddComment($" Clear Return Value:");
                                AddInstruction(Opcode.POP_VALUE);
                            }
                            else
                            {
                                AddInstruction(Opcode.DEBUG_SET_TAG, "Return value");
                            }
                        }
                    }
                    else
                    {
                        GenerateCodeForLiteralString(function.BuiltinName);

                        int offset = -1;

                        AddComment($" Param {0}:");
                        GenerateCodeForStatement(keywordCall.Parameters[0]);
                        offset--;

                        AddInstruction(Opcode.PUSH_VALUE, 0, "ID Length");
                        offset--;

                        AddComment($" Load Function Name String Pointer:");
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, offset);

                        AddComment(" .:");
                        AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                        bool thereIsReturnValue = false;
                        if (function.ReturnSomething)
                        {
                            if (!keywordCall.SaveValue)
                            {
                                AddComment($" Clear Return Value:");
                                AddInstruction(Opcode.POP_VALUE);
                            }
                            else
                            { thereIsReturnValue = true; }
                        }

                        AddComment(" Deallocate Function Name String:");

                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                        AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
                        AddInstruction(Opcode.HEAP_DEALLOC);

                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                        AddInstruction(Opcode.HEAP_DEALLOC);

                        if (thereIsReturnValue)
                        {
                            AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, -2);
                            AddInstruction(Opcode.DEBUG_SET_TAG, "Return value");
                        }
                        else
                        {
                            AddInstruction(Opcode.POP_VALUE);
                        }
                    }

                    AddComment("}");
                    return;
                }

                int returnValueSize = 0;
                if (function.ReturnSomething)
                {
                    returnValueSize = GenerateInitialValue(function.Type, "returnvalue");
                }

                AddComment($" Param {0}:");
                GenerateCodeForStatement(keywordCall.Parameters[0]);
                AddInstruction(Opcode.DEBUG_SET_TAG, $"param.{function.Parameters[0].Identifier}");

                AddComment(" .:");

                if (function.InstructionOffset == -1)
                { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(GeneratedCode.Count, keywordCall, this.parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

                AddInstruction(Opcode.CALL, function.InstructionOffset - GeneratedCode.Count);

                AddComment(" Clear Params:");

                AddInstruction(Opcode.POP_VALUE);

                if (function.ReturnSomething && !keywordCall.SaveValue)
                {
                    AddComment(" Clear Return Value:");
                    for (int i = 0; i < returnValueSize; i++)
                    { AddInstruction(Opcode.POP_VALUE); }
                }

                AddComment("}");

                return;
            }

            throw new CompilerException($"Unknown keyword-function \"{keywordCall.FunctionName}\"", keywordCall.Identifier, CurrentFile);
        }
        void GenerateCodeForStatement(FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "sizeof")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": requied {1} passed {functionCall.Parameters.Length}", functionCall.TotalPosition(), CurrentFile); }

                StatementWithValue param0 = functionCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                AddInstruction(Opcode.PUSH_VALUE, param0Type.SizeOnHeap, $"sizeof({param0Type.Name})");

                functionCall.Identifier = functionCall.Identifier.BuiltinFunction("sizeof", "int", new string[] { "p" }, new string[] { "any" });

                return;
            }

            if (functionCall.FunctionName == "Alloc")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"Alloc\": requied {1} passed {functionCall.Parameters.Length}", functionCall.TotalPosition(), CurrentFile); }

                GenerateCodeForStatement(functionCall.Parameters[0]);

                AddInstruction(Opcode.HEAP_ALLOC, "(pointer)");

                functionCall.Identifier = functionCall.Identifier.BuiltinFunction("Alloc", "int", new string[] { "int" }, new string[] { "size" });

                return;
            }

            if (GetVariable(functionCall.Identifier.Content, out CompiledVariable compiledVariable))
            {
                if (!compiledVariable.Type.IsFunction)
                { throw new CompilerException($"Variable \"{compiledVariable.VariableName.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

                if (compiledVariable.Type.Function.Function == null)
                { throw new InternalException(); }

                GenerateCodeForFunctionCall_Variable(functionCall, compiledVariable);
                return;
            }

            if (!GetFunction(functionCall, out CompiledFunction compiledFunction))
            { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

            GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);
        }

        void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
        {
            AddComment($"Call {compiledFunction.ReadableID()} {{");

            functionCall.Identifier = functionCall.Identifier.Function(compiledFunction);

            if (!compiledFunction.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The {compiledFunction.ReadableID()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
                AddComment("}");
                return;
            }

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ReadableID()}: requied {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {(compiledFunction.IsMethod ? "method" : "function")} \"{functionCall.FunctionName}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

            if (compiledFunction.IsBuiltin)
            {
                if (!BuiltinFunctions.TryGetValue(compiledFunction.BuiltinName, out var builtinFunction))
                {
                    Errors.Add(new Error($"Builtin function \"{compiledFunction.BuiltinName}\" not found", functionCall.Identifier, CurrentFile));
                    AddComment("}");
                    return;
                }

                AddComment(" Function Name:");
                if (BuiltinFunctionCache.TryGetValue(compiledFunction.BuiltinName, out int cacheAddress))
                {
                    if (compiledFunction.BuiltinName.Length == 0)
                    { throw new CompilerException($"Builtin function with length of zero", (FunctionDefinition.Attribute)compiledFunction.Attributes.Get("Builtin"), compiledFunction.FilePath); }

                    if (functionCall.PrevStatement != null)
                    {
                        AddComment(" Param prev:");
                        GenerateCodeForStatement(functionCall.PrevStatement);
                    }
                    for (int i = 0; i < functionCall.Parameters.Length; i++)
                    {
                        AddComment($" Param {i}:");
                        GenerateCodeForStatement(functionCall.Parameters[i]);
                    }
                    AddInstruction(Opcode.PUSH_VALUE, compiledFunction.BuiltinName.Length, "ID Length");

                    AddComment($" Load Function Name String Pointer (Cache):");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                    if (compiledFunction.ReturnSomething)
                    {
                        if (!functionCall.SaveValue)
                        {
                            AddComment($" Clear Return Value:");
                            AddInstruction(Opcode.POP_VALUE);
                        }
                        else
                        {
                            AddInstruction(Opcode.DEBUG_SET_TAG, "Return value");
                        }
                    }
                }
                else
                {
                    GenerateCodeForLiteralString(compiledFunction.BuiltinName);

                    int offset = -1;
                    if (functionCall.PrevStatement != null)
                    {
                        AddComment(" Param prev:");
                        GenerateCodeForStatement(functionCall.PrevStatement);
                        offset--;
                    }
                    for (int i = 0; i < functionCall.Parameters.Length; i++)
                    {
                        AddComment($" Param {i}:");
                        GenerateCodeForStatement(functionCall.Parameters[i]);
                        offset--;
                    }
                    AddInstruction(Opcode.PUSH_VALUE, 0, "ID Length");
                    offset--;

                    AddComment($" Load Function Name String Pointer:");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, offset);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                    bool thereIsReturnValue = false;
                    if (compiledFunction.ReturnSomething)
                    {
                        if (!functionCall.SaveValue)
                        {
                            AddComment($" Clear Return Value:");
                            AddInstruction(Opcode.POP_VALUE);
                        }
                        else
                        { thereIsReturnValue = true; }
                    }

                    AddComment(" Deallocate Function Name String:");

                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                    AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    if (thereIsReturnValue)
                    {
                        AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, -2);
                        AddInstruction(Opcode.DEBUG_SET_TAG, "Return value");
                    }
                    else
                    {
                        AddInstruction(Opcode.POP_VALUE);
                    }
                }

                AddComment("}");
                return;
            }

            int returnValueSize = 0;
            if (compiledFunction.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(compiledFunction.Type, "returnvalue");
            }

            if (functionCall.PrevStatement != null)
            {
                AddComment(" Param prev:");
                // TODO: variable sized prev statement
                GenerateCodeForStatement(functionCall.PrevStatement);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");
            }

            int paramsSize = 0;

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                Statement param = functionCall.Parameters[i];
                ParameterDefinition definedParam = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];

                AddComment($" Param {i}:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.Identifier);

                if (!Constants.BuiltinTypes.Contains(definedParam.Type.Identifier.Content))
                {
                    ITypeDefinition paramType = GetCustomType(definedParam.Type.ToString());
                    if (paramType is CompiledStruct @struct)
                    {
                        paramsSize += @struct.Size;
                    }
                    else if (paramType is CompiledClass)
                    {
                        paramsSize++;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            AddComment(" .:");

            if (compiledFunction.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(GeneratedCode.Count, functionCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

            AddInstruction(Opcode.CALL, compiledFunction.InstructionOffset - GeneratedCode.Count);

            AddComment(" Clear Params:");
            for (int i = 0; i < paramsSize; i++)
            {
                AddInstruction(Opcode.POP_VALUE);
            }

            if (functionCall.PrevStatement != null)
            {
                // TODO: variable sized prev statement
                AddInstruction(Opcode.POP_VALUE);
            }

            if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }
        void GenerateCodeForFunctionCall_Variable(FunctionCall functionCall, CompiledVariable compiledVariable)
        {
            AddComment($"Call {compiledVariable.Type.Function.Function.ReadableID()} {{");

            CompiledFunction compiledFunction = compiledVariable.Type.Function.Function;

            functionCall.Identifier = functionCall.Identifier.Variable(compiledVariable);
            functionCall.Identifier.AnalysedType = TokenAnalysedType.VariableName;

            if (!compiledFunction.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The {compiledFunction.ReadableID()} function cannot be called due to its protection level", functionCall.Identifier, CurrentFile));
                AddComment("}");
                return;
            }

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to {compiledFunction.ReadableID()}: requied {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall.TotalPosition(), CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {(compiledFunction.IsMethod ? "method" : "function")} \"{functionCall.FunctionName}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall.TotalPosition(), CurrentFile); }

            if (compiledFunction.IsBuiltin)
            {
                if (!BuiltinFunctions.TryGetValue(compiledFunction.BuiltinName, out var builtinFunction))
                {
                    Errors.Add(new Error($"Builtin function '{compiledFunction.BuiltinName}' not found", functionCall.Identifier, CurrentFile));
                    AddComment("}");
                    return;
                }

                AddComment(" Function Name:");
                if (BuiltinFunctionCache.TryGetValue(compiledFunction.BuiltinName, out int cacheAddress))
                {
                    if (compiledFunction.BuiltinName.Length == 0)
                    { throw new CompilerException($"Builtin function with length of zero", (FunctionDefinition.Attribute)compiledFunction.Attributes.Get("Builtin"), compiledFunction.FilePath); }

                    if (functionCall.PrevStatement != null)
                    {
                        AddComment(" Param prev:");
                        GenerateCodeForStatement(functionCall.PrevStatement);
                    }
                    for (int i = 0; i < functionCall.Parameters.Length; i++)
                    {
                        AddComment($" Param {i}:");
                        GenerateCodeForStatement(functionCall.Parameters[i]);
                    }
                    AddInstruction(Opcode.PUSH_VALUE, compiledFunction.BuiltinName.Length, "ID Length");

                    AddComment($" Load Function Name String Pointer (Cache):");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                    if (compiledFunction.ReturnSomething)
                    {
                        if (!functionCall.SaveValue)
                        {
                            AddComment($" Clear Return Value:");
                            AddInstruction(Opcode.POP_VALUE);
                        }
                        else
                        {
                            AddInstruction(Opcode.DEBUG_SET_TAG, "Return value");
                        }
                    }
                }
                else
                {
                    GenerateCodeForLiteralString(compiledFunction.BuiltinName);

                    int offset = -1;
                    if (functionCall.PrevStatement != null)
                    {
                        AddComment(" Param prev:");
                        GenerateCodeForStatement(functionCall.PrevStatement);
                        offset--;
                    }
                    for (int i = 0; i < functionCall.Parameters.Length; i++)
                    {
                        AddComment($" Param {i}:");
                        GenerateCodeForStatement(functionCall.Parameters[i]);
                        offset--;
                    }
                    AddInstruction(Opcode.PUSH_VALUE, 0, "ID Length");
                    offset--;

                    AddComment($" Load Function Name String Pointer:");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, offset);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                    bool thereIsReturnValue = false;
                    if (compiledFunction.ReturnSomething)
                    {
                        if (!functionCall.SaveValue)
                        {
                            AddComment($" Clear Return Value:");
                            AddInstruction(Opcode.POP_VALUE);
                        }
                        else
                        { thereIsReturnValue = true; }
                    }

                    AddComment(" Deallocate Function Name String:");

                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                    AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    if (thereIsReturnValue)
                    {
                        AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, -2);
                        AddInstruction(Opcode.DEBUG_SET_TAG, "Return value");
                    }
                    else
                    {
                        AddInstruction(Opcode.POP_VALUE);
                    }
                }

                AddComment("}");
                return;
            }

            int returnValueSize = 0;
            if (compiledFunction.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(compiledFunction.Type, "returnvalue");
            }

            if (functionCall.PrevStatement != null)
            {
                AddComment(" Param prev:");
                // TODO: variable sized prev statement
                GenerateCodeForStatement(functionCall.PrevStatement);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");
            }

            int paramsSize = 0;

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                Statement param = functionCall.Parameters[i];
                ParameterDefinition definedParam = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];

                AddComment($" Param {i}:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.Identifier);

                if (!Constants.BuiltinTypes.Contains(definedParam.Type.Identifier.Content))
                {
                    ITypeDefinition paramType = GetCustomType(definedParam.Type.ToString());
                    if (paramType is CompiledStruct @struct)
                    {
                        paramsSize += @struct.Size;
                    }
                    else if (paramType is CompiledClass)
                    {
                        paramsSize++;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            AddComment(" .:");

            if (compiledVariable.IsStoredInHEAP)
            { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, compiledVariable.MemoryAddress); }
            else
            { LoadFromStack(compiledVariable.MemoryAddress, compiledVariable.Type.Size, !compiledVariable.IsGlobal); }

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.CALL, AddressingMode.RUNTIME);

            AddComment(" Clear Params:");
            for (int i = 0; i < paramsSize; i++)
            {
                AddInstruction(Opcode.POP_VALUE);
            }

            if (functionCall.PrevStatement != null)
            {
                // TODO: variable sized prev statement
                AddInstruction(Opcode.POP_VALUE);
            }

            if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }

        void GenerateCodeForStatement(OperatorCall @operator)
        {
            if (OptimizeCode)
            {
                DataItem? predictedValueN = PredictStatementValue(@operator);
                if (predictedValueN.HasValue)
                {
                    var predictedValue = predictedValueN.Value;

                    switch (predictedValue.type)
                    {
                        case RuntimeType.BYTE:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueByte);
                                Informations.Add(new Information($"Predicted value: {predictedValue.ValueByte}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case RuntimeType.INT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueInt);
                                Informations.Add(new Information($"Predicted value: {predictedValue.ValueInt}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case RuntimeType.FLOAT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueFloat);
                                Informations.Add(new Information($"Predicted value: {predictedValue.ValueFloat}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        default: throw new NotImplementedException();
                    }
                }
            }

            if (GetOperator(@operator, out CompiledOperator operatorDefinition))
            {
                AddComment($"Call {operatorDefinition.Identifier} {{");

                if (!operatorDefinition.CanUse(CurrentFile))
                {
                    Errors.Add(new Error($"The {operatorDefinition.ReadableID()} operator cannot be called due to its protection level", @operator.Operator, CurrentFile));
                    AddComment("}");
                    return;
                }

                if (@operator.ParameterCount != operatorDefinition.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator {operatorDefinition.ReadableID()}: requied {operatorDefinition.ParameterCount} passed {@operator.ParameterCount}", @operator.TotalPosition(), CurrentFile); }

                if (operatorDefinition.IsBuiltin)
                {
                    if (!BuiltinFunctions.TryGetValue(operatorDefinition.BuiltinName, out var builtinFunction))
                    {
                        Errors.Add(new Error($"Builtin function \"{operatorDefinition.BuiltinName}\" not found", @operator.Operator, CurrentFile));
                        AddComment("}");
                        return;
                    }

                    AddComment(" Function Name:");
                    if (BuiltinFunctionCache.TryGetValue(operatorDefinition.BuiltinName, out int cacheAddress))
                    { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress); }
                    else
                    { GenerateCodeForLiteralString(operatorDefinition.BuiltinName); }

                    int offset = -1;
                    for (int i = 0; i < @operator.Parameters.Length; i++)
                    {
                        AddComment($" Param {i}:");
                        GenerateCodeForStatement(@operator.Parameters[i]);
                        offset--;
                    }

                    AddComment($" Load Function Name String Pointer:");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, offset);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                    bool thereIsReturnValue = false;
                    if (!@operator.SaveValue)
                    {
                        AddComment($" Clear Return Value:");
                        AddInstruction(Opcode.POP_VALUE);
                    }
                    else
                    { thereIsReturnValue = true; }

                    AddComment(" Deallocate Function Name String:");

                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                    AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, thereIsReturnValue ? -2 : -1);
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    if (thereIsReturnValue)
                    {
                        AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, -2);
                        AddInstruction(Opcode.DEBUG_SET_TAG, "Return value");
                    }
                    else
                    {
                        AddInstruction(Opcode.POP_VALUE);
                    }

                    AddComment("}");
                    return;
                }

                int returnValueSize = GenerateInitialValue(operatorDefinition.Type, "returnvalue");

                int paramsSize = 0;

                for (int i = 0; i < @operator.Parameters.Length; i++)
                {
                    Statement param = @operator.Parameters[i];
                    ParameterDefinition definedParam = operatorDefinition.Parameters[i];

                    AddComment($" Param {i}:");
                    GenerateCodeForStatement(param);
                    AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.Identifier);

                    if (!Constants.BuiltinTypes.Contains(definedParam.Type.Identifier.Content))
                    {
                        ITypeDefinition paramType = GetCustomType(definedParam.Type.ToString());
                        if (paramType is CompiledStruct @struct)
                        {
                            paramsSize += @struct.Size;
                        }
                        else if (paramType is CompiledClass)
                        {
                            paramsSize++;
                        }
                    }
                    else
                    {
                        paramsSize++;
                    }
                }

                AddComment(" .:");

                if (operatorDefinition.InstructionOffset == -1)
                { UndefinedOperatorFunctionOffsets.Add(new UndefinedOperatorFunctionOffset(GeneratedCode.Count, @operator, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

                AddInstruction(Opcode.CALL, operatorDefinition.InstructionOffset - GeneratedCode.Count);

                AddComment(" Clear Params:");
                for (int i = 0; i < paramsSize; i++)
                {
                    AddInstruction(Opcode.POP_VALUE);
                }

                if (!@operator.SaveValue)
                {
                    AddComment(" Clear Return Value:");
                    for (int i = 0; i < returnValueSize; i++)
                    { AddInstruction(Opcode.POP_VALUE); }
                }

                AddComment("}");
            }
            else if (Constants.Operators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (Constants.Operators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': requied {Constants.Operators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }

                GenerateCodeForStatement(@operator.Left);
                if (@operator.Right != null) GenerateCodeForStatement(@operator.Right);
                AddInstruction(opcode);
            }
            else if (@operator.Operator.Content == "=")
            {
                if (@operator.ParameterCount != 2)
                { throw new CompilerException($"Wrong number of parameters passed to assigment operator '{@operator.Operator.Content}': requied {2} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }

                GenerateCodeForValueSetter(@operator.Left, @operator.Right);
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }
        }
        void GenerateCodeForStatement(Assignment setter)
        {
            GenerateCodeForValueSetter(setter.Left, setter.Right);
        }
        void GenerateCodeForStatement(BBCode.Parser.Statement.Literal literal)
        {
            switch (literal.Type)
            {
                case LiteralType.INT:
                    AddInstruction(Opcode.PUSH_VALUE, int.Parse(literal.Value));
                    break;
                case LiteralType.FLOAT:
                    AddInstruction(Opcode.PUSH_VALUE, float.Parse(literal.Value.TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case LiteralType.STRING:
                    GenerateCodeForLiteralString(literal);
                    break;
                case LiteralType.BOOLEAN:
                    AddInstruction(Opcode.PUSH_VALUE, (bool.Parse(literal.Value) ? 1 : 0));
                    break;
                case LiteralType.CHAR:
                    if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", CurrentFile);
                    AddInstruction(Opcode.PUSH_VALUE, literal.Value[0]);
                    break;
                default: throw new NotImplementedException();
            }
        }

        void GenerateCodeForLiteralString(BBCode.Parser.Statement.Literal literal)
            => GenerateCodeForLiteralString(literal.Value);
        void GenerateCodeForLiteralString(string literal)
        {
            AddComment($"Create String \"{literal}\" {{");

            AddComment("Allocate String object {");

            AddInstruction(Opcode.PUSH_VALUE, 2, $"sizeof(String)");
            AddInstruction(Opcode.HEAP_ALLOC, $"(pointer String)");

            // Allocate object fields
            {
                ValueTuple<string, CompiledType>[] fields = new ValueTuple<string, CompiledType>[]
                {
                    new ValueTuple<string, CompiledType>("pointer", new CompiledType(BBCode.Compiler.Type.INT)),
                    new ValueTuple<string, CompiledType>("length", new CompiledType(BBCode.Compiler.Type.INT)),
                };
                int currentOffset = 0;
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var field = fields[fieldIndex];
                    AddComment($"Create Field '{field.Item1}' ({fieldIndex}) {{");
                    GenerateInitialValue(field.Item2, j =>
                    {
                        AddComment($"Save Chunk {j}:");
                        AddInstruction(Opcode.PUSH_VALUE, currentOffset);
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -3);
                        AddInstruction(Opcode.MATH_ADD);
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                        currentOffset++;
                    }, $"String.{field.Item1}");
                    AddComment("}");
                }
            }

            AddComment("}");

            AddComment("Set String.pointer {");

            // Set String.pointer
            {
                AddInstruction(Opcode.PUSH_VALUE, literal.Length, $"sizeof(\"{literal}\")");
                AddInstruction(Opcode.HEAP_ALLOC, $"(subpointer String)");

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
                AddInstruction(Opcode.PUSH_VALUE, 0);
                AddInstruction(Opcode.MATH_ADD);

                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");

            AddComment("Set String.length {");
            // Set String.length
            {
                AddInstruction(Opcode.PUSH_VALUE, literal.Length);

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
                AddInstruction(Opcode.PUSH_VALUE, 1);
                AddInstruction(Opcode.MATH_ADD);

                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");

            AddComment("Set string data {");
            for (int i = 0; i < literal.Length; i++)
            {
                // Prepare value
                AddInstruction(Opcode.PUSH_VALUE, literal[i]);

                // Load String.pointer
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
                AddInstruction(Opcode.PUSH_VALUE, 0);
                AddInstruction(Opcode.MATH_ADD);
                AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);

                // Offset the pointer
                AddInstruction(Opcode.PUSH_VALUE, i);
                AddInstruction(Opcode.MATH_ADD);

                // Set value
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");

            AddComment("}");
        }

        void GenerateCodeForStatement(Identifier variable)
        {
            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                variable.VariableName = variable.VariableName.Variable(param);

                int offset = GetDataAddress(param);

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);
            }
            else if (GetVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                variable.VariableName = variable.VariableName.Variable(val);

                if (val.IsStoredInHEAP)
                {
                    AddInstruction(Opcode.LOAD_VALUE, val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, val.MemoryAddress);
                }
                else
                {
                    LoadFromStack(val.MemoryAddress, val.Type.Size, !val.IsGlobal);
                }
            }
            else if (GetFunction(variable, out var compiledFunction))
            {
                if (compiledFunction.InstructionOffset == -1)
                { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(GeneratedCode.Count, variable, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

                AddInstruction(Opcode.PUSH_VALUE, compiledFunction.InstructionOffset, $"(function) {compiledFunction.ReadableID()}");

                variable.VariableName.AnalysedType = TokenAnalysedType.FunctionName;
            }
            else
            {
                throw new CompilerException($"Variable \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
            }
        }
        void GenerateCodeForStatement(AddressGetter memoryAddressGetter)
        {
            void GetVariableAddress(Identifier variable)
            {
                if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
                {
                    variable.VariableName = variable.VariableName.Variable(param);

                    if (param.Type.IsClass)
                    {
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, param.RealIndex - 1);
                    }
                    else
                    {
                        AddInstruction(Opcode.GET_BASEPOINTER);
                        AddInstruction(Opcode.PUSH_VALUE, param.RealIndex);
                        AddInstruction(Opcode.MATH_ADD);
                    }
                }
                else if (GetVariable(variable.VariableName.Content, out CompiledVariable val))
                {
                    variable.VariableName = variable.VariableName.Variable(val);

                    if (val.IsStoredInHEAP)
                    {
                        AddInstruction(Opcode.PUSH_VALUE, val.MemoryAddress);
                    }
                    else
                    {
                        if (val.IsGlobal)
                        {
                            AddInstruction(Opcode.PUSH_VALUE, val.MemoryAddress);
                        }
                        else
                        {
                            AddInstruction(Opcode.GET_BASEPOINTER);
                            AddInstruction(Opcode.PUSH_VALUE, val.MemoryAddress);
                            AddInstruction(Opcode.MATH_ADD);
                        }
                    }
                }
                else
                {
                    throw new CompilerException($"Variable \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
                }
            }

            void GetFieldAddress(Field field)
            {
                var prevType = FindStatementType(field.PrevStatement);

                if (prevType.IsStruct)
                {
                    int offset = GetDataAddress(field, out AddressingMode addressingMode);
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    if (addressingMode == AddressingMode.BASEPOINTER_RELATIVE)
                    {
                        AddInstruction(Opcode.GET_BASEPOINTER);
                        AddInstruction(Opcode.MATH_ADD);
                    }
                    return;
                }

                if (prevType.IsClass)
                {
                    int offset = GetFieldOffset(field);
                    int pointerOffset = GetBaseAddress(field, out AddressingMode addressingMode);
                    GenerateCodeForValueGetter(pointerOffset, addressingMode);
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    AddInstruction(Opcode.MATH_ADD);
                    return;
                }

                throw new NotImplementedException();
            }

            void GetAddress(Statement statement)
            {
                if (statement is Identifier variable)
                {
                    GetVariableAddress(variable);
                    return;
                }

                if (statement is Field field)
                {
                    GetFieldAddress(field);
                    return;
                }

                throw new NotImplementedException();
            }

            GetAddress(memoryAddressGetter.PrevStatement);
        }
        void GenerateCodeForStatement(Pointer memoryAddressFinder)
        {
            GenerateCodeForStatement(memoryAddressFinder.PrevStatement);
            // TODO: stack getter
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }
        void GenerateCodeForStatement(WhileLoop whileLoop)
        {
            var conditionValue = PredictStatementValue(whileLoop.Condition);
            if (conditionValue.HasValue && conditionValue.Value.IsFalsy() && TrimUnreachableCode)
            {
                AddComment("Unreachable code not compiled");
                Informations.Add(new Information($"Unreachable code not compiled", new Position(whileLoop.Block), CurrentFile));
                return;
            }

            AddComment("while (...) {");
            AddComment("Condition");
            int conditionOffset = GeneratedCode.Count;
            GenerateCodeForStatement(whileLoop.Condition);

            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffset = GeneratedCode.Count - 1;

            BreakInstructions.Add(new List<int>());

            AddComment("Statements {");
            for (int i = 0; i < whileLoop.Block.Statements.Count; i++)
            {
                GenerateCodeForStatement(whileLoop.Block.Statements[i]);
            }

            AddComment("}");

            AddComment("Jump Back");
            AddInstruction(Opcode.JUMP_BY, conditionOffset - GeneratedCode.Count);

            AddComment("}");

            GeneratedCode[conditionJumpOffset].Parameter = new DataItem(GeneratedCode.Count - conditionJumpOffset);
            List<int> currentBreakInstructions = BreakInstructions.Last();

            if (currentBreakInstructions.Count == 0)
            {
                if (conditionValue.HasValue)
                {
                    if (!conditionValue.Value.IsFalsy())
                    { Warnings.Add(new Warning($"Potential infinity loop", whileLoop.Keyword, CurrentFile)); }
                    else
                    { Warnings.Add(new Warning($"Bruh", whileLoop.Keyword, CurrentFile)); }
                }
            }

            foreach (var breakInstruction in currentBreakInstructions)
            {
                GeneratedCode[breakInstruction].Parameter = new DataItem(GeneratedCode.Count - breakInstruction);
            }
            BreakInstructions.RemoveAt(BreakInstructions.Count - 1);
        }
        void GenerateCodeForStatement(ForLoop forLoop)
        {
            AddComment("for (...) {");

            AddComment("FOR Declaration");
            // Index variable
            GenerateCodeForVariable(forLoop.VariableDeclaration, false);
            CleanupStack.Push(new CleanupItem(1, 1));
            GenerateCodeForStatement(forLoop.VariableDeclaration);

            AddComment("FOR Condition");
            // Index condition
            int conditionOffsetFor = GeneratedCode.Count;
            GenerateCodeForStatement(forLoop.Condition);
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffsetFor = GeneratedCode.Count - 1;

            BreakInstructions.Add(new List<int>());

            AddComment("Statements {");
            for (int i = 0; i < forLoop.Block.Statements.Count; i++)
            {
                Statement currStatement = forLoop.Block.Statements[i];
                GenerateCodeForStatement(currStatement);
            }

            AddComment("}");

            AddComment("FOR Expression");
            // Index expression
            GenerateCodeForStatement(forLoop.Expression);

            AddComment("Jump back");
            AddInstruction(Opcode.JUMP_BY, conditionOffsetFor - GeneratedCode.Count);
            GeneratedCode[conditionJumpOffsetFor].Parameter = new DataItem(GeneratedCode.Count - conditionJumpOffsetFor);

            foreach (var breakInstruction in BreakInstructions.Last())
            {
                GeneratedCode[breakInstruction].Parameter = new DataItem(GeneratedCode.Count - breakInstruction);
            }
            BreakInstructions.RemoveAt(BreakInstructions.Count - 1);

            CleanupVariables(CleanupStack.Pop());

            AddComment("}");
        }
        void GenerateCodeForStatement(IfContainer @if)
        {
            List<int> jumpOutInstructions = new();

            foreach (var ifSegment in @if.Parts)
            {
                if (ifSegment is IfBranch partIf)
                {
                    AddComment("if (...) {");

                    AddComment("IF Condition");
                    GenerateCodeForStatement(partIf.Condition);
                    AddComment("IF Jump to Next");
                    int jumpNextInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    CleanupStack.Push(CompileVariables(partIf.Block, false));

                    AddComment("IF Statements");
                    for (int i = 0; i < partIf.Block.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partIf.Block.Statements[i]);
                    }

                    CleanupVariables(CleanupStack.Pop());

                    AddComment("IF Jump to End");
                    jumpOutInstructions.Add(GeneratedCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddComment("}");

                    GeneratedCode[jumpNextInstruction].Parameter = new DataItem(GeneratedCode.Count - jumpNextInstruction);
                }
                else if (ifSegment is ElseIfBranch partElseif)
                {
                    AddComment("elseif (...) {");

                    AddComment("ELSEIF Condition");
                    GenerateCodeForStatement(partElseif.Condition);
                    AddComment("ELSEIF Jump to Next");
                    int jumpNextInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    CleanupStack.Push(CompileVariables(partElseif.Block, false));

                    AddComment("ELSEIF Statements");
                    for (int i = 0; i < partElseif.Block.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElseif.Block.Statements[i]);
                    }

                    CleanupVariables(CleanupStack.Pop());

                    AddComment("IF Jump to End");
                    jumpOutInstructions.Add(GeneratedCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddComment("}");

                    GeneratedCode[jumpNextInstruction].Parameter = new DataItem(GeneratedCode.Count - jumpNextInstruction);
                }
                else if (ifSegment is ElseBranch partElse)
                {
                    AddComment("else {");

                    AddComment("ELSE Statements");

                    CleanupStack.Push(CompileVariables(partElse.Block, false));

                    for (int i = 0; i < partElse.Block.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElse.Block.Statements[i]);
                    }

                    CleanupVariables(CleanupStack.Pop());

                    AddComment("}");
                }
            }

            foreach (var item in jumpOutInstructions)
            {
                GeneratedCode[item].Parameter = new DataItem(GeneratedCode.Count - item);
            }
        }
        void GenerateCodeForStatement(NewInstance newObject)
        {
            AddComment($"new {newObject.TypeName} {{");

            ITypeDefinition instanceType = GetCustomType(newObject.TypeName.Content, newObject.TypeName.GetPosition());

            if (instanceType is CompiledStruct @struct)
            {
                newObject.TypeName = newObject.TypeName.Struct(@struct);
                @struct.References?.Add(new DefinitionReference(newObject.TypeName, CurrentFile));

                throw new NotImplementedException();
            }
            else if (instanceType is CompiledClass @class)
            {
                newObject.TypeName = newObject.TypeName.Class(@class);
                @class.References?.Add(new DefinitionReference(newObject.TypeName, CurrentFile));

                AddInstruction(Opcode.PUSH_VALUE, @class.Size, $"sizeof({@class.Name.Content})");
                AddInstruction(Opcode.HEAP_ALLOC, $"(pointer {@class.Name.Content})");
                int currentOffset = 0;
                for (int fieldIndex = 0; fieldIndex < @class.Fields.Length; fieldIndex++)
                {
                    CompiledField field = @class.Fields[fieldIndex];
                    AddComment($"Create Field '{field.Identifier.Content}' ({fieldIndex}) {{");
                    GenerateInitialValue(field.Type, j =>
                    {
                        AddComment($"Save Chunk {j}:");
                        AddInstruction(Opcode.PUSH_VALUE, currentOffset);
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -3);
                        AddInstruction(Opcode.MATH_ADD);
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                        currentOffset++;
                    }, $"{@class.Name.Content}.{field.Identifier.Content}");
                    AddComment("}");
                }
            }
            else
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", newObject.TypeName, CurrentFile); }
            AddComment("}");
        }
        void GenerateCodeForStatement(ConstructorCall constructorCall)
        {
            AddComment($"new {constructorCall.TypeName}(...): {{");

            var instanceType = GetCustomType(constructorCall.TypeName.Content, constructorCall.TypeName.GetPosition());

            if (instanceType is CompiledStruct)
            { throw new NotImplementedException(); }

            if (instanceType is not CompiledClass @class)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }


            constructorCall.TypeName = constructorCall.TypeName.Class(@class);
            @class.References?.Add(new DefinitionReference(constructorCall.TypeName, CurrentFile));

            if (!GetConstructor(constructorCall, out var constructor))
            {
                throw new CompilerException($"Constructor for type \"{constructorCall.TypeName}\" not found", constructorCall.TypeName, CurrentFile);
            }

            if (!constructor.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The \"{constructorCall.TypeName}\" constructor cannot be called due to its protection level", constructorCall.Keyword, CurrentFile));
                AddComment("}");
                return;
            }

            if (constructorCall.Parameters.Length != constructor.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to \"{constructorCall.TypeName}\" constructor: requied {constructor.ParameterCount} passed {constructorCall.Parameters.Length}", constructorCall.TotalPosition(), CurrentFile); }

            int returnValueSize = 0;
            if (constructor.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(constructor.Type, "returnvalue");
            }

            int paramsSize = 0;

            for (int i = 0; i < constructorCall.Parameters.Length; i++)
            {
                Statement param = constructorCall.Parameters[i];
                ParameterDefinition definedParam = constructor.Parameters[constructor.IsMethod ? (i + 1) : i];

                AddComment($" Param {i}:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.Identifier);

                if (!Constants.BuiltinTypes.Contains(definedParam.Type.Identifier.Content))
                {
                    ITypeDefinition paramType = GetCustomType(definedParam.Type.ToString());
                    if (paramType is CompiledStruct @struct_)
                    {
                        paramsSize += @struct_.Size;
                    }
                    else if (paramType is CompiledClass)
                    {
                        paramsSize++;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            AddComment(" .:");

            if (constructor.InstructionOffset == -1)
            { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(GeneratedCode.Count, constructorCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

            AddInstruction(Opcode.CALL, constructor.InstructionOffset - GeneratedCode.Count);

            AddComment(" Clear Params:");
            for (int i = 0; i < paramsSize; i++)
            {
                AddInstruction(Opcode.POP_VALUE);
            }

            if (constructor.ReturnSomething && !constructorCall.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }
        void GenerateCodeForStatement(Field field)
        {
            field.FieldName.AnalysedType = TokenAnalysedType.FieldName;

            var prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsEnum)
            {
                AddInstruction(Opcode.PUSH_VALUE, prevType.Enum.Members.Get<string, CompiledEnumMember>(field.FieldName.Content).Value);
                return;
            }

            if (!prevType.IsStruct && !prevType.IsClass) throw new NotImplementedException();

            var type = FindStatementType(field);

            if (prevType.IsClass)
            {
                if (!GetCompiledField(field, out var compiledField))
                { throw new CompilerException($"Field definition \"{field.FieldName}\" not found in class \"{prevType.Class.Name}\"", field, CurrentFile); }

                if (CurrentContext.Context != null)
                {
                    switch (compiledField.Protection)
                    {
                        case Protection.Private:
                            if (CurrentContext.Context.Name.Content != compiledField.Class.Name.Content)
                            { throw new CompilerException($"Can not access field \"{compiledField.Identifier.Content}\" of class \"{compiledField.Class.Name}\" due to it's protection level", field, CurrentFile); }
                            break;
                        case Protection.Public:
                            break;
                        default: throw new NotImplementedException();
                    }
                }
            }

            if (prevType.IsStruct)
            {
                field.FieldName = field.FieldName.Field(prevType.Struct, field, type);
            }
            else if (prevType.IsClass)
            {
                field.FieldName = field.FieldName.Field(prevType.Class, field, type);
            }

            bool _inHeap = IsItInHeap(field);

            if (_inHeap)
            {
                int offset = GetFieldOffset(field);
                int pointerOffset = GetBaseAddress(field, out AddressingMode addressingMode);
                for (int i = 0; i < type.SizeOnStack; i++)
                {
                    AddComment($"{i}:");
                    GenerateCodeForValueGetter(pointerOffset, offset + i, addressingMode);
                }
            }
            else
            {
                int offset = GetDataAddress(field, out AddressingMode addressingMode);
                GenerateCodeForValueGetter(offset, addressingMode);
            }
        }
        void GenerateCodeForStatement(IndexCall index)
        {
            CompiledType prevType = FindStatementType(index.PrevStatement);

            if (!prevType.IsClass)
            { throw new CompilerException($"Index getter for type \"{prevType.Name}\" not found", index, CurrentFile); }

            if (!GetIndexGetter(prevType.Class, out CompiledFunction indexer))
            { throw new CompilerException($"Index getter for class \"{prevType.Class.Name}\" not found", index, CurrentFile); }

            AddComment($"Call {indexer.ReadableID()} {{");

            if (!indexer.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The {indexer.ReadableID()} function could not be called due to its protection level", index.GetPosition(), CurrentFile));
                AddComment("}");
                return;
            }

            if (indexer.IsBuiltin)
            { throw new NotImplementedException(); }

            int returnValueSize = GenerateInitialValue(indexer.Type, "returnvalue");

            AddComment(" Param prev:");
            GenerateCodeForStatement(index.PrevStatement);
            AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");

            Statement param = index.Expression;
            ParameterDefinition definedParam = indexer.Parameters[indexer.IsMethod ? (0 + 1) : 0];

            AddComment($" Expression:");
            GenerateCodeForStatement(param);
            AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.Identifier);

            int paramsSize = 1;
            if (!Constants.BuiltinTypes.Contains(definedParam.Type.Identifier.Content))
            {
                ITypeDefinition paramType = GetCustomType(definedParam.Type.ToString(), definedParam.Type.GetPosition());
                if (paramType is CompiledStruct @struct)
                { paramsSize = @struct.Size; }
            }

            AddComment(" .:");

            if (indexer.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(GeneratedCode.Count, index, false, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

            AddInstruction(Opcode.CALL, indexer.InstructionOffset - GeneratedCode.Count);

            AddComment(" Clear Params:");
            Pop(paramsSize);

            AddInstruction(Opcode.POP_VALUE);

            if (!index.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }
        void GenerateCodeForStatement(LiteralList listValue)
        { throw new NotImplementedException(); }
        void GenerateCodeForStatement(TypeCast @as)
        {
            GenerateCodeForStatement(@as.PrevStatement);

            CompiledType type = FindStatementType(@as.PrevStatement);
            CompiledType targetType = new(@as.Type, GetCustomType);

            if (type == targetType)
            {
                Hints.Add(new Hint($"Redundant type conversion", @as.Keyword, CurrentFile));
                return;
            }

            if (type.IsBuiltin && targetType.IsBuiltin)
            {
                AddInstruction(Opcode.PUSH_VALUE, (byte)targetType.BuiltinType.Convert(), $"typecast target type");
                AddInstruction(Opcode.TYPE_SET);
            }
        }

        void GenerateCodeForStatement(Statement st)
        {
            DebugInfo debugInfo = new()
            {
                InstructionStart = GeneratedCode.Count,
                InstructionEnd = GeneratedCode.Count,
            };
            if (st is StatementWithBlock statementParent)
            { CleanupStack.Push(CompileVariables(statementParent.Block, false)); }

            if (st is LiteralList listValue)
            { GenerateCodeForStatement(listValue); }
            else if (st is VariableDeclaretion newVariable)
            { GenerateCodeForStatement(newVariable); }
            else if (st is FunctionCall functionCall)
            { GenerateCodeForStatement(functionCall); }
            else if (st is KeywordCall keywordCall)
            { GenerateCodeForStatement(keywordCall); }
            else if (st is OperatorCall @operator)
            { GenerateCodeForStatement(@operator); }
            else if (st is AnyAssignment setter)
            { GenerateCodeForStatement(setter.ToAssignment()); }
            else if (st is BBCode.Parser.Statement.Literal literal)
            { GenerateCodeForStatement(literal); }
            else if (st is Identifier variable)
            { GenerateCodeForStatement(variable); }
            else if (st is AddressGetter memoryAddressGetter)
            { GenerateCodeForStatement(memoryAddressGetter); }
            else if (st is Pointer memoryAddressFinder)
            { GenerateCodeForStatement(memoryAddressFinder); }
            else if (st is WhileLoop whileLoop)
            { GenerateCodeForStatement(whileLoop); }
            else if (st is ForLoop forLoop)
            { GenerateCodeForStatement(forLoop); }
            else if (st is IfContainer @if)
            { GenerateCodeForStatement(@if); }
            else if (st is NewInstance newStruct)
            { GenerateCodeForStatement(newStruct); }
            else if (st is ConstructorCall constructorCall)
            { GenerateCodeForStatement(constructorCall); }
            else if (st is IndexCall indexStatement)
            { GenerateCodeForStatement(indexStatement); }
            else if (st is Field field)
            { GenerateCodeForStatement(field); }
            else if (st is TypeCast @as)
            { GenerateCodeForStatement(@as); }
            else
            {
                Output.Debug.Debug.Log("[Compiler]: Unimplemented statement " + st.GetType().Name);
            }

            if (st is StatementWithBlock)
            { CleanupVariables(CleanupStack.Pop()); }

            debugInfo.InstructionEnd = GeneratedCode.Count - 1;
            debugInfo.Position = st.TotalPosition();
            GeneratedDebugInfo.Add(debugInfo);
        }

        void GenerateCodeForValueGetter(int offset, AddressingMode addressingMode)
        {
            switch (addressingMode)
            {
                case AddressingMode.ABSOLUTE:
                case AddressingMode.BASEPOINTER_RELATIVE:
                case AddressingMode.RELATIVE:
                    AddInstruction(Opcode.LOAD_VALUE, addressingMode, offset);
                    break;

                case AddressingMode.POP:
                    AddInstruction(Opcode.LOAD_VALUE, addressingMode);
                    break;

                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    AddInstruction(Opcode.LOAD_VALUE, addressingMode);
                    break;
                default: throw new NotImplementedException();
            }
        }
        void GenerateCodeForValueGetter(int pointerOffset, int offset, AddressingMode addressingMode)
        {
            GenerateCodeForValueGetter(pointerOffset, addressingMode);
            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }

        void GenerateCodeForValueSetter(int offset, AddressingMode addressingMode)
        {
            switch (addressingMode)
            {
                case AddressingMode.ABSOLUTE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.ABSOLUTE, offset);
                    break;
                case AddressingMode.BASEPOINTER_RELATIVE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);
                    break;
                case AddressingMode.RELATIVE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, offset);
                    break;
                case AddressingMode.POP:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.POP);
                    break;
                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RUNTIME);
                    break;
                default: throw new NotImplementedException();
            }
        }
        void GenerateCodeForValueSetter(int pointerOffset, int offset, AddressingMode addressingMode)
        {
            GenerateCodeForValueGetter(pointerOffset, addressingMode);
            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }

        CleanupItem CompileVariables(Block block, bool isGlobal, bool addComments = true)
        {
            if (addComments) AddComment("Variables");
            int count = 0;
            int variableSizeSum = 0;
            foreach (var s in block.Statements)
            {
                var v = GenerateCodeForVariable(s, isGlobal);
                variableSizeSum += v.Size;
                count += v.Count;
            }
            return new CleanupItem(variableSizeSum, count);
        }
        void Pop(int n, string comment = null)
        {
            if (n <= 0) return;
            if (comment != null) AddComment(comment);
            for (int x = 0; x < n; x++)
            { AddInstruction(Opcode.POP_VALUE); }
        }
        void PopVariable(int n)
        {
            for (int x = 0; x < n; x++)
            { compiledVariables.Remove(compiledVariables.Last().Key); }
        }

        void CleanupVariables(CleanupItem cleanupItem)
        {
            Pop(cleanupItem.Size, "Clear variables");
            PopVariable(cleanupItem.Count);
        }

        void GenerateCodeForValueSetter(Statement statementToSet, StatementWithValue value)
        {
            if (statementToSet is Identifier variable)
            { GenerateCodeForValueSetter(variable, value); }
            else if (statementToSet is Field field)
            { GenerateCodeForValueSetter(field, value); }
            else if (statementToSet is IndexCall index)
            { GenerateCodeForValueSetter(index, value); }
            else if (statementToSet is Pointer memoryAddressGetter)
            { GenerateCodeForValueSetter(memoryAddressGetter, value); }
            else
            { throw new CompilerException($"The left side of the assignment operator should be a variable, field or memory address. Passed {statementToSet.GetType().Name}", statementToSet, CurrentFile); }
        }
        void GenerateCodeForValueSetter(Identifier statementToSet, StatementWithValue value)
        {
            CompiledType valueType = FindStatementType(value);

            if (GetParameter(statementToSet.VariableName.Content, out CompiledParameter parameter))
            {
                statementToSet.VariableName = statementToSet.VariableName.Variable(parameter);

                if (parameter.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{parameter.Type.Name}\" type parameter.", value, CurrentFile); }

                GenerateCodeForStatement(value);
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
            }
            else if (GetVariable(statementToSet.VariableName.Content, out CompiledVariable variable))
            {
                statementToSet.VariableName = statementToSet.VariableName.Variable(variable);

                if (variable.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{variable.Type.Name}\" type variable.", value, CurrentFile); }

                GenerateCodeForStatement(value);
                AddInstruction(Opcode.STORE_VALUE, variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
            }
            else
            {
                throw new CompilerException($"Variable \"{statementToSet.VariableName.Content}\" not found", statementToSet.VariableName, CurrentFile);
            }
        }
        void GenerateCodeForValueSetter(Field statementToSet, StatementWithValue value)
        {
            statementToSet.FieldName.AnalysedType = TokenAnalysedType.FieldName;

            CompiledType valueType = FindStatementType(value);

            var prevType = FindStatementType(statementToSet.PrevStatement);
            if (!prevType.IsStruct && !prevType.IsClass)
            {
                throw new NotImplementedException();
            }

            var type = FindStatementType(statementToSet);
            if (prevType.IsStruct)
            { statementToSet.FieldName = statementToSet.FieldName.Field(prevType.Struct, statementToSet, type); }
            else if (prevType.IsClass)
            { statementToSet.FieldName = statementToSet.FieldName.Field(prevType.Class, statementToSet, type); }

            if (type != valueType)
            { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{type.Name}\" type field.", value, CurrentFile); }

            GenerateCodeForStatement(value);

            bool _inHeap = IsItInHeap(statementToSet);

            if (_inHeap)
            {
                int offset = GetFieldOffset(statementToSet);
                int pointerOffset = GetBaseAddress(statementToSet, out AddressingMode addressingMode);
                GenerateCodeForValueSetter(pointerOffset, offset, addressingMode);
                return;
            }
            else
            {
                int offset = GetDataAddress(statementToSet, out AddressingMode addressingMode);
                GenerateCodeForValueSetter(offset, addressingMode);
                return;
            }

            throw new NotImplementedException();
        }
        void GenerateCodeForValueSetter(IndexCall statementToSet, StatementWithValue value)
        {
            CompiledType prevType = FindStatementType(statementToSet.PrevStatement);
            CompiledType valueType = FindStatementType(value);

            if (!prevType.IsClass)
            { throw new CompilerException($"Index setter for type \"{prevType.Name}\" not found", statementToSet, CurrentFile); }

            if (!GetIndexSetter(prevType.Class, valueType, out CompiledFunction indexer))
            { throw new CompilerException($"Index setter for class \"{prevType.Class.Name}\" not found", statementToSet, CurrentFile); }

            AddComment($"Call {indexer.ReadableID()} {{");

            if (!indexer.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The {indexer.ReadableID()} function could not be called due to its protection level", statementToSet.GetPosition(), CurrentFile));
                AddComment("}");
                return;
            }

            if (indexer.IsBuiltin)
            { throw new NotImplementedException(); }

            AddComment(" Param prev:");
            GenerateCodeForStatement(statementToSet.PrevStatement);
            AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");

            Statement param = statementToSet.Expression;
            ParameterDefinition definedParam = indexer.Parameters[indexer.IsMethod ? 1 : 0];

            AddComment($" Expression:");
            GenerateCodeForStatement(param);
            AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.Identifier);

            int paramsSize = 1;
            if (!Constants.BuiltinTypes.Contains(definedParam.Type.Identifier.Content))
            {
                ITypeDefinition paramType = GetCustomType(definedParam.Type.ToString(), definedParam.Type.GetPosition());
                if (paramType is CompiledStruct @struct)
                { paramsSize += @struct.Size; }
                else
                { paramsSize += 1; }
            }
            else
            { paramsSize += 1; }

            if (valueType.IsStruct)
            { paramsSize += valueType.Struct.Size; }
            else
            { paramsSize += 1; }

            AddComment(" .:");

            if (indexer.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(GeneratedCode.Count, statementToSet, true, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

            AddInstruction(Opcode.CALL, indexer.InstructionOffset - GeneratedCode.Count);

            AddComment(" Clear expression value:");
            Pop(paramsSize);

            AddInstruction(Opcode.POP_VALUE);

            AddComment("}");
        }
        void GenerateCodeForValueSetter(Pointer statementToSet, StatementWithValue value)
        {
            CompiledType targetType = FindStatementType(statementToSet);

            if (targetType.SizeOnStack != 1) throw new NotImplementedException();

            GenerateCodeForStatement(value);
            GenerateCodeForStatement(statementToSet.PrevStatement);

            // TODO: set value by stack address
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }
        void GenerateCodeForValueSetter(VariableDeclaretion statementToSet, StatementWithValue value)
        {
            if (value is LiteralList)
            { throw new NotImplementedException(); }

            if (!GetVariable(statementToSet.VariableName.Content, out var variable))
            { throw new CompilerException($"Variable \"{statementToSet.VariableName.Content}\" not found", statementToSet.VariableName, CurrentFile); }

            statementToSet.VariableName = statementToSet.VariableName.Variable(variable);

            CompiledType valueType = FindStatementType(value);

            AssignTypeCheck(variable.Type, valueType, value);

            GenerateCodeForStatement(value);

            if (valueType.InHEAP)
            {
                AddInstruction(Opcode.STORE_VALUE, variable.AddressingMode(), variable.MemoryAddress);
            }
            else
            {
                CopyToStack(variable.MemoryAddress, variable.Type.Size, !variable.IsGlobal);
                for (int i = 0; i < variable.Type.Size; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }
        }

        void AssignTypeCheck(CompiledType destination, CompiledType valueType, StatementWithValue value)
        {
            if (destination == valueType)
            { return; }

            if (destination.IsEnum)
            { if (CodeGeneratorBase.SameType(destination.Enum, valueType)) return; }

            if (valueType.IsEnum)
            { if (CodeGeneratorBase.SameType(valueType.Enum, destination)) return; }

            throw new CompilerException($"Can not set a {valueType.Name} type value to the {destination.Name} type variable.", value, CurrentFile);
        }

        #endregion

        #region HEAP Helpers

        void CopyToHeap(int to, int size)
        {
            AddComment($"Copy to heap: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddComment($"Element {currentOffset}:");

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, (-currentReversedOffset) - 1);

                AddInstruction(Opcode.PUSH_VALUE, to + size);
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");
        }
        void CopyToStack(int to, int size, bool basepointerRelative)
        {
            AddComment($"Copy to stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddComment($"Element {currentOffset}:");

                int loadFrom = (-currentReversedOffset) - 1;
                int storeTo = to + currentOffset;

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, loadFrom);

                AddInstruction(Opcode.STORE_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, storeTo);
            }
            AddComment("}");
        }

        void LoadFromHeap(int from, int size)
        {
            AddComment($"Load from heap: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");

                AddInstruction(Opcode.PUSH_VALUE, currentOffset + from);
                AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
            }
            AddComment("}");
        }
        void LoadFromStack(int from, int size, bool basepointerRelative)
        {
            AddComment($"Load from stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");

                int loadFrom = from + currentOffset;

                AddInstruction(Opcode.LOAD_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, loadFrom);
            }
            AddComment("}");
        }

        /*
        void Deallocate(CompiledVariable variable)
        {
            if (!variable.IsStoredInHEAP) return;
            AddInstruction(Opcode.LOAD_VALUE, variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
            AddInstruction(Opcode.HEAP_DEALLOC);
            return;
            AddComment("Deallocate {");
            for (int offset = 0; offset < variable.Type.Size; offset++)
            {
                AddInstruction(Opcode.PUSH_VALUE, DataItem.Null);
                AddInstruction(Opcode.PUSH_VALUE, offset);
                AddInstruction(Opcode.LOAD_VALUE, variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
                AddInstruction(Opcode.MATH_ADD);
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");
        }

        void Deallocate(int from, int length)
        {
            AddComment("Deallocate {");

            AddInstruction(Opcode.PUSH_VALUE, from);
            AddInstruction(Opcode.PUSH_VALUE, length);

            AddComment("While _length_ != 0 :");
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1);
            int skipIfZeroInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, int.MaxValue);

            AddComment($"Decrement _length_:");
            AddInstruction(Opcode.PUSH_VALUE, -1);
            AddInstruction(Opcode.MATH_ADD);

            AddComment("Null value:");
            AddInstruction(Opcode.PUSH_VALUE, DataItem.Null);

            AddComment("Calculate address (_length_ + _start_):");
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -4);
            AddInstruction(Opcode.MATH_ADD);

            AddComment("Store null value:");
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);

            AddComment("Jump back to loop:");
            AddInstruction(Opcode.JUMP_BY, skipIfZeroInstruction - GeneratedCode.Count - 1);

            GeneratedCode[skipIfZeroInstruction].Parameter = GeneratedCode.Count - skipIfZeroInstruction;

            AddComment("Cleanup parameters:");
            AddInstruction(Opcode.POP_VALUE);
            AddInstruction(Opcode.POP_VALUE);

            AddComment("}");
        }

        /// <summary>
        /// <b>Expected Stack:</b> <br/><br/>
        /// <code>
        /// ... <br/>
        /// <see langword="int"/> FROM <br/>
        /// <see langword="int"/> LENGTH <br/>
        /// </code>
        /// This will pop these two stack elements.
        /// </summary>
        void DynamicDeallocateWithParametersAlreadyExists()
        {
            AddInstruction(Opcode.POP_VALUE);
            AddInstruction(Opcode.HEAP_DEALLOC);
            return;

            AddComment("Deallocate {");

            AddComment("While _length_ != 0 :");
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1);
            int skipIfZeroInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, int.MaxValue);

            AddComment($"Decrement _length_:");
            AddInstruction(Opcode.PUSH_VALUE, -1);
            AddInstruction(Opcode.MATH_ADD);

            AddComment("Null value:");
            AddInstruction(Opcode.PUSH_VALUE, DataItem.Null);

            AddComment("Calculate address (_length_ + _start_):");
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -4);
            AddInstruction(Opcode.MATH_ADD);

            AddComment("Store null value:");
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);

            AddComment("Jump back to loop:");
            AddInstruction(Opcode.JUMP_BY, skipIfZeroInstruction - GeneratedCode.Count - 1);

            GeneratedCode[skipIfZeroInstruction].Parameter = GeneratedCode.Count - skipIfZeroInstruction;

            AddComment("Cleanup parameters:");
            AddInstruction(Opcode.POP_VALUE);
            AddInstruction(Opcode.POP_VALUE);

            AddComment("}");
        }

        void DynamicDeallocate(StatementWithReturnValue from, StatementWithReturnValue length)
        {
            GenerateCodeForStatement(from);
            AddInstruction(Opcode.HEAP_DEALLOC);

            return;
            AddComment("Dynamic Deallocate {");

            GenerateCodeForStatement(from);
            GenerateCodeForStatement(length);

            AddComment("While _length_ != 0 :");
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1);
            int skipIfZeroInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, int.MaxValue);

            AddComment($"Decrement _length_:");
            AddInstruction(Opcode.PUSH_VALUE, -1);
            AddInstruction(Opcode.MATH_ADD);

            AddComment("Null value:");
            AddInstruction(Opcode.PUSH_VALUE, DataItem.Null);

            AddComment("Calculate address (_length_ + _start_):");
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -4);
            AddInstruction(Opcode.MATH_ADD);

            AddComment("Store null value:");
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);

            AddComment("Jump back to loop:");
            AddInstruction(Opcode.JUMP_BY, skipIfZeroInstruction - GeneratedCode.Count - 1);

            GeneratedCode[skipIfZeroInstruction].Parameter = GeneratedCode.Count - skipIfZeroInstruction;

            AddComment("Cleanup parameters:");
            AddInstruction(Opcode.POP_VALUE);
            AddInstruction(Opcode.POP_VALUE);

            AddComment("}");
        }
        */

        #endregion

        #region Adressing Helpers

        int GetReturnValueAddress() => 0 - (ParameterSizeSum() + 2);

        /// <summary>
        /// Returns the variable's <b>memory address on the stack</b>
        /// </summary>
        /// <param name="addressingMode">
        /// Can be:<br/><see cref="AddressingMode.BASEPOINTER_RELATIVE"/><br/> or <br/><see cref="AddressingMode.ABSOLUTE"/>
        /// </param>
        /// <exception cref="InternalException"/>
        /// <exception cref="CompilerException"/>
        int GetDataAddress(Identifier variable, out AddressingMode addressingMode)
        {
            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                return GetDataAddress(param);
            }

            if (GetVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                if (val.IsStoredInHEAP)
                { throw new InternalException($"This should never occur: trying to GetDataAddress of variable \"{val.VariableName.Content}\" wich's type is {val.Type}"); }

                addressingMode = val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE;

                return val.MemoryAddress;
            }

            throw new CompilerException($"Variable \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
        }

        /// <summary>
        /// Returns the field's <b>memory address on the stack</b>
        /// </summary>
        /// <param name="addressingMode">
        /// Can be:<br/><see cref="AddressingMode.BASEPOINTER_RELATIVE"/><br/> or <br/><see cref="AddressingMode.ABSOLUTE"/>
        /// </param>
        /// <exception cref="NotImplementedException"/>
        int GetDataAddress(Field field, out AddressingMode addressingMode)
        {
            var prevType = FindStatementType(field.PrevStatement);
            if (prevType.IsStruct)
            {
                var @struct = prevType.Struct;
                if (@struct.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Identifier _prevVar)
                    {
                        if (GetParameter(_prevVar.VariableName.Content, out CompiledParameter param))
                        {
                            addressingMode = AddressingMode.BASEPOINTER_RELATIVE;

                            return GetDataAddress(param, fieldOffset);
                        }
                        return fieldOffset + GetDataAddress(_prevVar, out addressingMode);
                    }
                    if (field.PrevStatement is Field _prevField) return fieldOffset + GetDataAddress(_prevField, out addressingMode);
                }
            }
            else if (prevType.IsClass)
            {
                var @class = prevType.Class;
                if (@class.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Identifier _prevVar)
                    {
                        if (GetParameter(_prevVar.VariableName.Content, out CompiledParameter param))
                        {
                            addressingMode = AddressingMode.BASEPOINTER_RELATIVE;

                            return GetDataAddress(param, fieldOffset);
                        }
                        return fieldOffset + GetDataAddress(_prevVar, out addressingMode);
                    }
                    if (field.PrevStatement is Field prevField) return fieldOffset + GetDataAddress(prevField, out addressingMode);
                }
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the <paramref name="field"/>'s offset relative to the base object/field
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        int GetFieldOffset(Field field)
        {
            var prevType = FindStatementType(field.PrevStatement);
            if (prevType.IsStruct)
            {
                var @struct = prevType.Struct;
                if (@struct.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Identifier) return fieldOffset;
                    if (field.PrevStatement is Field prevField) return fieldOffset + GetFieldOffset(prevField);
                }
            }
            else if (prevType.IsClass)
            {
                var @class = prevType.Class;
                if (@class.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Identifier) return fieldOffset;
                    if (field.PrevStatement is Field prevField) return fieldOffset + GetFieldOffset(prevField);
                }
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the <paramref name="parameter"/>'s <b>memory address on the stack</b>
        /// </summary>
        int GetDataAddress(CompiledParameter parameter) => 0 - (ParameterSizeSum(parameter.Index) + 1);
        /// <summary>
        /// Returns the <paramref name="parameter"/>'s offsetted <b>memory address on the stack</b>
        /// </summary>
        int GetDataAddress(CompiledParameter parameter, int offset) => 0 - ((ParameterSizeSum(parameter.Index) - offset) + 1);
        /// <summary>
        /// Returns the <paramref name="variable"/>'s <b>memory address on the stack</b>
        /// </summary>
        /// <param name="addressingMode">
        /// Can be:<br/><see cref="AddressingMode.BASEPOINTER_RELATIVE"/><br/> or <br/><see cref="AddressingMode.ABSOLUTE"/>
        /// </param>
        /// <exception cref="CompilerException"/>
        int GetBaseAddress(Identifier variable, out AddressingMode addressingMode)
        {
            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                return GetDataAddress(param);
            }

            if (GetVariable(variable.VariableName.Content, out CompiledVariable val))
            { return GetBaseAddress(val, out addressingMode); }

            throw new CompilerException($"Variable \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
        }
        /// <summary>
        /// Returns the <paramref name="variable"/>'s <b>memory address on the stack</b>
        /// </summary>
        /// <param name="addressingMode">
        /// Can be:<br/><see cref="AddressingMode.BASEPOINTER_RELATIVE"/><br/> or <br/><see cref="AddressingMode.ABSOLUTE"/>
        /// </param>
        /// <exception cref="CompilerException"/>
        static int GetBaseAddress(CompiledVariable variable, out AddressingMode addressingMode)
        {
            addressingMode = variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE;
            return variable.MemoryAddress;
        }
        /// <summary>
        /// Returns the <paramref name="field"/>'s base memory address. In the most cases the memory address is at the pointer that points to the <paramref name="field"/>'s object on the heap.
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        int GetBaseAddress(Field field, out AddressingMode addressingMode)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsStruct || prevType.IsClass)
            {
                if (field.PrevStatement is Identifier prevVariable) return GetBaseAddress(prevVariable, out addressingMode);
                if (field.PrevStatement is Field prevField) return GetBaseAddress(prevField, out addressingMode);
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks if the <paramref name="variable"/> is stored on the heap or not
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="CompilerException"/>
        bool IsItInHeap(Identifier variable)
        {
            if (GetParameter(variable.VariableName.Content, out var parameter))
            {
                if (parameter.Type.IsStruct) return false;
                if (parameter.Type.IsClass) return true;

                throw new NotImplementedException();
            }

            if (GetVariable(variable.VariableName.Content, out CompiledVariable val))
            { return val.IsStoredInHEAP; }

            throw new CompilerException($"Variable \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
        }
        /// <summary>
        /// Checks if the <paramref name="field"/> is stored on the heap or not
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        bool IsItInHeap(Field field)
        {
            var prevType = FindStatementType(field.PrevStatement);
            if (prevType.IsStruct)
            {
                if (field.PrevStatement is Identifier _prevVar) return IsItInHeap(_prevVar);
                if (field.PrevStatement is Field _prevField) return IsItInHeap(_prevField);
            }
            else if (prevType.IsClass)
            {
                if (field.PrevStatement is Identifier _prevVar) return IsItInHeap(_prevVar);
                if (field.PrevStatement is Field _prevField) return IsItInHeap(_prevField);
            }

            throw new NotImplementedException();
        }

        #endregion

        #region GenerateCodeFor...

        /// <returns>The variable's size</returns>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateCodeForVariable(VariableDeclaretion newVariable, bool isGlobal)
        {
            if (newVariable.Type.Identifier == "var")
            {
                if (newVariable.InitialValue != null)
                {
                    if (newVariable.InitialValue is BBCode.Parser.Statement.Literal literal)
                    {
                        newVariable.Type = TypeInstance.CreateAnonymous(literal.Type, TypeDefinitionReplacer);
                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);
                    }
                    else if (newVariable.InitialValue is NewInstance newInstance)
                    {
                        newVariable.Type.Identifier.Content = newInstance.TypeName.Content;
                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);
                    }
                    else if (newVariable.InitialValue is ConstructorCall constructorCall)
                    {
                        newVariable.Type.Identifier.Content = constructorCall.TypeName.Content;
                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);
                    }
                    else
                    {
                        CompiledType initialTypeRaw = FindStatementType(newVariable.InitialValue);
                        newVariable.Type = TypeInstance.CreateAnonymous(initialTypeRaw);

                        newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);

                        GenerateCodeForVariable(newVariable, isGlobal);
                        return 1;
                    }
                }
                else
                { throw new CompilerException($"Initial value for 'var' variable declaration is requied", newVariable, newVariable.FilePath); }

                if (newVariable.Type.Identifier == "var")
                { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }

                GenerateCodeForVariable(newVariable, isGlobal);
                return 1;
            }

            newVariable.VariableName = newVariable.VariableName.Variable(newVariable.VariableName.Content, newVariable.Type.ToString(), false);

            if (isGlobal)
            { compiledVariables.Add(newVariable.VariableName.Content, GetVariableInfo(newVariable, GetVariableSizesSum(true) + BuiltinFunctionCache.Count, isGlobal)); }
            else
            { compiledVariables.Add(newVariable.VariableName.Content, GetVariableInfo(newVariable, GetVariableSizesSum(false), isGlobal)); }

            AddComment($"Initial value {{");

            int size = GenerateInitialValue(newVariable.Type, "var." + newVariable.VariableName.Content);

            AddComment("}");

            return size;
        }
        CleanupItem GenerateCodeForVariable(Statement st, bool isGlobal)
        {
            if (st is VariableDeclaretion newVariable)
            {
                int size = GenerateCodeForVariable(newVariable, isGlobal);
                return new CleanupItem(size, 1);
            }
            return new CleanupItem(0, 0);
        }
        CleanupItem GenerateCodeForVariable(Statement[] sts, bool isGlobal)
        {
            int count = 0;
            int size = 0;
            for (int i = 0; i < sts.Length; i++)
            {
                var v = GenerateCodeForVariable(sts[i], isGlobal);
                size += v.Size;
                count += v.Count;
            }
            return new CleanupItem(size, count);
        }

        int GetVariableSizesSum(bool alsoGlobals)
        {
            int sum = 0;
            for (int i = 0; i < compiledVariables.Count; i++)
            {
                var key = compiledVariables.ElementAt(i).Key;
                if (compiledVariables.Get(key).IsGlobal && !alsoGlobals) continue;
                if (compiledVariables.Get(key).Type.IsClass) sum++;
                else sum += compiledVariables.Get(key).Type.Size;
            }
            return sum;
        }

        void ClearLocalVariables()
        {
            for (int i = compiledVariables.Count - 1; i >= 0; i--)
            {
                var key = compiledVariables.ElementAt(i).Key;
                if (compiledVariables.Get(key).IsGlobal) continue;
                compiledVariables.Remove(key);
            }
        }

        void GenerateCodeForFunction(CompiledFunction function)
        {
            if (Constants.Keywords.Contains(function.Identifier.Content))
            { throw new CompilerException($"The identifier \"{function.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.FilePath); }

            function.Identifier.AnalysedType = TokenAnalysedType.FunctionName;

            if (function.IsBuiltin) return;

            parameters.Clear();
            ClearLocalVariables();
            ReturnInstructions.Clear();

            // Compile parameters
            int paramIndex = 0;
            int paramsSize = 0;
            foreach (ParameterDefinition parameter in function.Parameters)
            {
                paramIndex++;
                parameters.Add(new CompiledParameter(paramIndex, paramsSize, function.Parameters.Length, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter));

                if (!Constants.BuiltinTypes.Contains(parameter.Type.Identifier.Content))
                {
                    var paramType = GetCustomType(parameter.Type.Identifier.Content);
                    if (paramType is CompiledStruct @struct)
                    {
                        paramsSize += @struct.Size;
                    }
                    else if (paramType is CompiledClass @class)
                    {
                        paramsSize += 1;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            CurrentFile = function.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"{function.ReadableID()};{CurrentFile};{GeneratedCode.Count};{function.Identifier.Position.Start.Line}");

            // Search for variables
            AddComment("Variables");
            CleanupStack.Push(GenerateCodeForVariable(function.Statements, false));

            // Compile statements
            if (function.Statements.Length > 0)
            {
                AddComment("Statements");
                foreach (Statement statement in function.Statements)
                {
                    GenerateCodeForStatement(statement);
                }
            }

            CurrentFile = null;

            int cleanupCodeOffset = GeneratedCode.Count;

            for (int i = 0; i < ReturnInstructions.Count; i++)
            { GeneratedCode[ReturnInstructions[i]].Parameter = new DataItem(cleanupCodeOffset - ReturnInstructions[i]); }

            AddComment("Cleanup {");

            CleanupVariables(CleanupStack.Pop());

            AddComment("}");

            AddComment("Return");
            AddInstruction(Opcode.CS_POP);
            AddInstruction(Opcode.RETURN);

            parameters.Clear();
            ClearLocalVariables();
            ReturnInstructions.Clear();
        }

        void GenerateCodeForFunction(CompiledOperator function)
        {
            if (Constants.Keywords.Contains(function.Identifier.Content))
            { throw new CompilerException($"Illegal function name '{function.Identifier.Content}'", function.Identifier, function.FilePath); }

            function.Identifier.AnalysedType = TokenAnalysedType.FunctionName;

            if (function.IsBuiltin) return;

            parameters.Clear();
            ClearLocalVariables();
            ReturnInstructions.Clear();

            // Compile parameters
            int paramIndex = 0;
            int paramsSize = 0;
            foreach (ParameterDefinition parameter in function.Parameters)
            {
                paramIndex++;
                parameters.Add(new CompiledParameter(paramIndex, paramsSize, function.Parameters.Length, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter));

                if (!Constants.BuiltinTypes.Contains(parameter.Type.Identifier.Content))
                {
                    var paramType = GetCustomType(parameter.Type.Identifier.Content);
                    if (paramType is CompiledStruct @struct)
                    {
                        paramsSize += @struct.Size;
                    }
                    else if (paramType is CompiledClass @class)
                    {
                        paramsSize += 1;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            CurrentFile = function.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"{function.ReadableID()};{CurrentFile};{GeneratedCode.Count};{function.Identifier.Position.Start.Line}");

            // Search for variables
            AddComment("Variables");
            CleanupStack.Push(GenerateCodeForVariable(function.Statements, false));

            // Compile statements
            if (function.Statements.Length > 0)
            {
                AddComment("Statements");
                foreach (Statement statement in function.Statements)
                {
                    GenerateCodeForStatement(statement);
                }
            }

            CurrentFile = null;

            int cleanupCodeOffset = GeneratedCode.Count;

            for (int i = 0; i < ReturnInstructions.Count; i++)
            { GeneratedCode[ReturnInstructions[i]].Parameter = new DataItem(cleanupCodeOffset - ReturnInstructions[i]); }

            AddComment("Cleanup {");

            CleanupVariables(CleanupStack.Pop());

            AddComment("}");

            AddComment("Return");
            AddInstruction(Opcode.CS_POP);
            AddInstruction(Opcode.RETURN);

            parameters.Clear();
            ClearLocalVariables();
            ReturnInstructions.Clear();
        }

        void GenerateCodeForFunction(CompiledGeneralFunction function)
        {
            if (Constants.Keywords.Contains(function.Identifier.Content))
            { throw new CompilerException($"Illegal function name '{function.Identifier.Content}'", function.Identifier, function.FilePath); }

            function.Identifier.AnalysedType = TokenAnalysedType.FunctionName;

            parameters.Clear();
            ClearLocalVariables();
            ReturnInstructions.Clear();

            // Compile parameters
            int paramIndex = 0;
            int paramsSize = 0;
            foreach (ParameterDefinition parameter in function.Parameters)
            {
                paramIndex++;
                parameters.Add(new CompiledParameter(paramIndex, paramsSize, function.Parameters.Length, new CompiledType(parameter.Type, v => GetCustomType(v)), parameter));

                if (!Constants.BuiltinTypes.Contains(parameter.Type.Identifier.Content))
                {
                    var paramType = GetCustomType(parameter.Type.Identifier.Content);
                    if (paramType is CompiledStruct @struct)
                    {
                        paramsSize += @struct.Size;
                    }
                    else if (paramType is CompiledClass @class)
                    {
                        paramsSize += 1;
                    }
                }
                else
                {
                    paramsSize++;
                }
            }

            CurrentFile = function.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"{function.Type} {function.ReadableID()};{CurrentFile};{GeneratedCode.Count};{function.Identifier.Position.Start.Line}");

            // Search for variables
            AddComment("Variables");
            CleanupStack.Push(GenerateCodeForVariable(function.Statements, false));

            // Compile statements
            if (function.Statements.Length > 0)
            {
                AddComment("Statements");
                foreach (Statement statement in function.Statements)
                {
                    GenerateCodeForStatement(statement);
                }
            }

            CurrentFile = null;

            int cleanupCodeOffset = GeneratedCode.Count;

            for (int i = 0; i < ReturnInstructions.Count; i++)
            { GeneratedCode[ReturnInstructions[i]].Parameter = new DataItem(cleanupCodeOffset - ReturnInstructions[i]); }

            AddComment("Cleanup {");

            CleanupVariables(CleanupStack.Pop());

            AddComment("}");

            AddComment("Return");
            AddInstruction(Opcode.CS_POP);
            AddInstruction(Opcode.RETURN);

            parameters.Clear();
            ClearLocalVariables();
            ReturnInstructions.Clear();
        }

        #endregion

        CompiledVariable GetVariableInfo(VariableDeclaretion newVariable, int memoryOffset, bool isGlobal)
        {
            if (Constants.Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.Content}'", newVariable.VariableName, CurrentFile); }

            bool inHeap = GetClass(newVariable.Type.Identifier.Content, out _);
            CompiledType type = new(newVariable.Type, GetCustomType);

            return new CompiledVariable(
                memoryOffset,
                type,
                isGlobal,
                inHeap,
                newVariable);
        }

        #region Result Structs

        public struct Result
        {
            public Instruction[] Code;
            public DebugInfo[] DebugInfo;

            public CompiledFunction[] Functions;
            public CompiledOperator[] Operators;
            public CompiledGeneralFunction[] GeneralFunctions;

            public CompiledStruct[] Structs;
            public CompiledClass[] Classes;

            public Hint[] Hints;
            public Information[] Informations;
            public Warning[] Warnings;
            public Error[] Errors;
        }

        #endregion

        void GenerateCodeForTopLevelStatements(Statement[] statements)
        {
            CurrentFile = null;

            // Search for variables
            AddComment("Variables");
            CleanupStack.Push(GenerateCodeForVariable(statements, true));

            // Compile statements
            if (statements.Length > 0)
            {
                AddComment("Statements");
                foreach (Statement statement in statements)
                {
                    GenerateCodeForStatement(statement);
                }
            }

            int cleanupCodeOffset = GeneratedCode.Count;

            for (int i = 0; i < ReturnInstructions.Count; i++)
            { GeneratedCode[ReturnInstructions[i]].Parameter = new DataItem(cleanupCodeOffset - ReturnInstructions[i]); }

            AddComment("Cleanup {");

            CleanupVariables(CleanupStack.Pop());

            AddComment("}");
        }

        Result GenerateCode(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            Action<string, Output.LogType> printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
        {
            this.GenerateDebugInstructions = settings.GenerateDebugInstructions;
            this.AddCommentsToCode = settings.GenerateComments;
            this.GeneratedCode = new();
            this.BuiltinFunctions = compilerResult.BuiltinFunctions;
            this.BuiltinFunctionCache = new();
            this.OptimizeCode = !settings.DontOptimize;
            this.GeneratedDebugInfo.Clear();
            this.CleanupStack = new();
            this.ReturnInstructions = new();
            this.BreakInstructions = new();
            this.UndefinedFunctionOffsets = new();
            this.UndefinedOperatorFunctionOffsets = new();
            this.UndefinedGeneralFunctionOffsets = new();

            this.compiledVariables = new();
            this.parameters = new();

            this.Informations = new();
            this.Hints = new();
            base.Warnings = new();
            base.Errors = new();

            base.CompiledClasses = compilerResult.Classes;
            base.CompiledStructs = compilerResult.Structs;

            base.CompiledEnums = compilerResult.Enums;

            (
                this.CompiledFunctions,
                this.CompiledOperators,
                this.CompiledGeneralFunctions
            ) = UnusedFunctionManager.RemoveUnusedFunctions(
                    compilerResult,
                    settings.RemoveUnusedFunctionsMaxIterations,
                    printCallback,
                    level
                    );

            List<string> UsedBuiltinFunctions = new();

            foreach (CompiledFunction function in this.CompiledFunctions)
            {
                if (function.IsBuiltin)
                { UsedBuiltinFunctions.Add(function.BuiltinName); }
            }

            foreach (CompiledOperator @operator in this.CompiledOperators)
            {
                if (@operator.IsBuiltin)
                { UsedBuiltinFunctions.Add(@operator.BuiltinName); }
            }

            if (settings.BuiltinFunctionCache)
            {
                AddComment($"Create builtin function cache {{");
                foreach (string builtinFunction in UsedBuiltinFunctions)
                {
                    AddComment($"Create string \"{builtinFunction}\" {{");

                    AddInstruction(Opcode.PUSH_VALUE, builtinFunction.Length, $"sizeof(\"{builtinFunction}\")");
                    AddInstruction(Opcode.HEAP_ALLOC, $"(String pointer)");

                    BuiltinFunctionCache.Add(builtinFunction, BuiltinFunctionCache.Count);

                    for (int i = 0; i < builtinFunction.Length; i++)
                    {
                        // Prepare value
                        AddInstruction(Opcode.PUSH_VALUE, builtinFunction[i]);

                        // Calculate pointer
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
                        AddInstruction(Opcode.PUSH_VALUE, i);
                        AddInstruction(Opcode.MATH_ADD);

                        // Set value
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                    }

                    AddComment("}");
                }
                AddComment("}");
            }

            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

            int entryCallInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.CALL, -1);

            if (BuiltinFunctionCache.Count > 0)
            {
                AddComment("Clear builtin function cache {");
                for (int i = 0; i < BuiltinFunctionCache.Count; i++)
                {
                    AddInstruction(Opcode.HEAP_DEALLOC);
                }
                AddComment("}");
            }

            AddInstruction(Opcode.EXIT);

            foreach (var function in this.CompiledFunctions)
            {
                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                foreach (var attribute in function.Attributes)
                {
                    if (attribute.Identifier.Content != "CodeEntry") continue;
                    GeneratedCode[entryCallInstruction].Parameter = new DataItem(GeneratedCode.Count - entryCallInstruction);
                }

                AddComment(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Statements.Length > 0) ? "" : " }"));
                GenerateCodeForFunction(function);
                if (function.Statements.Length > 0) AddComment("}");
                CurrentContext = null;
            }

            foreach (var function in this.CompiledOperators)
            {
                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                AddComment(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Statements.Length > 0) ? "" : " }"));
                GenerateCodeForFunction(function);
                if (function.Statements.Length > 0) AddComment("}");
                CurrentContext = null;
            }

            foreach (var function in this.CompiledGeneralFunctions)
            {
                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                AddComment(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Statements.Length > 0) ? "" : " }"));
                GenerateCodeForFunction(function);
                if (function.Statements.Length > 0) AddComment("}");
                CurrentContext = null;
            }

            if (GeneratedCode[entryCallInstruction].ParameterInt == -1)
            {
                GeneratedCode[entryCallInstruction] = new Instruction(Opcode.COMMENT);
            }

            foreach (UndefinedFunctionOffset item in UndefinedFunctionOffsets)
            {
                foreach (var pair in item.currentParameters)
                { parameters.Add(pair); }
                foreach (var pair in item.currentVariables)
                { compiledVariables.Add(pair.Key, pair.Value); }

                CompiledFunction function;
                bool useAbsolute;

                if (item.CallStatement != null)
                {
                    if (!GetFunction(item.CallStatement, out function))
                    { throw new CompilerException($"Function {item.CallStatement.ReadableID(FindStatementType)} not found", item.CallStatement.Identifier, CurrentFile); }
                    useAbsolute = false;
                }
                else if (item.VariableStatement != null)
                {
                    if (!GetFunction(item.VariableStatement, out function))
                    { throw new CompilerException($"Function {item.VariableStatement}() not found", item.VariableStatement.VariableName, CurrentFile); }
                    useAbsolute = true;
                }
                else if (item.IndexStatement != null)
                {
                    CompiledType prevType = FindStatementType(item.IndexStatement.PrevStatement);
                    if (item.IsSetter)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        if (!prevType.IsClass)
                        { throw new CompilerException($"Index getter for type \"{prevType.Name}\" not found", item.IndexStatement, CurrentFile); }

                        if (!GetIndexGetter(prevType.Class, out function))
                        { throw new CompilerException($"Index getter for class \"{prevType.Class.Name}\" not found", item.IndexStatement, CurrentFile); }
                    }
                    useAbsolute = false;
                }
                else
                { throw new InternalException(); }

                if (function.InstructionOffset == -1)
                { throw new InternalException($"Function {function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                int offset = useAbsolute ? function.InstructionOffset : function.InstructionOffset - item.CallInstructionIndex;
                GeneratedCode[item.CallInstructionIndex].Parameter = new DataItem(offset);

                parameters.Clear();
                compiledVariables.Clear();
            }

            foreach (UndefinedOperatorFunctionOffset item in UndefinedOperatorFunctionOffsets)
            {
                foreach (var pair in item.currentParameters)
                { parameters.Add(pair); }
                foreach (var pair in item.currentVariables)
                { compiledVariables.Add(pair.Key, pair.Value); }

                if (!GetOperator(item.CallStatement, out var function))
                { throw new CompilerException($"Operator {item.CallStatement.ReadableID(FindStatementType)} not found", item.CallStatement.Operator, CurrentFile); }

                if (function.InstructionOffset == -1)
                { throw new InternalException($"Operator {function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                GeneratedCode[item.CallInstructionIndex].Parameter = new DataItem(function.InstructionOffset - item.CallInstructionIndex);

                parameters.Clear();
                compiledVariables.Clear();
            }

            foreach (UndefinedGeneralFunctionOffset item in UndefinedGeneralFunctionOffsets)
            {
                foreach (var pair in item.currentParameters)
                { parameters.Add(pair); }
                foreach (var pair in item.currentVariables)
                { compiledVariables.Add(pair.Key, pair.Value); }

                if (item.CallStatement is ConstructorCall constructorCall)
                {
                    if (!GetConstructor(constructorCall, out var function))
                    { throw new CompilerException($"Constructor for type \"{constructorCall.TypeName}\" not found", constructorCall.TypeName, CurrentFile); }

                    if (function.InstructionOffset == -1)
                    { throw new InternalException($"Constructor for type \"{constructorCall.TypeName}\" does not have instruction offset", item.CurrentFile); }

                    GeneratedCode[item.CallInstructionIndex].Parameter = new DataItem(function.InstructionOffset - item.CallInstructionIndex);
                }
                else if (item.CallStatement is KeywordCall functionCall)
                {
                    if (functionCall.Identifier.Content == "delete")
                    {
                        CompiledClass @class = FindStatementType(functionCall.Parameters[0]).Class;
                        if (!GetDestructor(@class ?? throw new NullReferenceException(), out var function))
                        { throw new CompilerException($"Constructor for type \"{@class}\" not found", functionCall, CurrentFile); }

                        if (function.InstructionOffset == -1)
                        { throw new InternalException($"Constructor for type \"{@class}\" does not have instruction offset", item.CurrentFile); }

                        GeneratedCode[item.CallInstructionIndex].Parameter = new DataItem(function.InstructionOffset - item.CallInstructionIndex);
                    }
                    else if (functionCall.Identifier.Content == "clone")
                    {
                        CompiledClass @class = FindStatementType(functionCall.Parameters[0]).Class;
                        if (!GetCloner(@class ?? throw new NullReferenceException(), out var function))
                        { throw new CompilerException($"Cloner for type \"{@class}\" not found", functionCall, CurrentFile); }

                        if (function.InstructionOffset == -1)
                        { throw new InternalException($"Cloner for type \"{@class}\" does not have instruction offset", item.CurrentFile); }

                        GeneratedCode[item.CallInstructionIndex].Parameter = new DataItem(function.InstructionOffset - item.CallInstructionIndex);
                    }
                    else if (functionCall.Identifier.Content == "out")
                    {
                        if (functionCall.Parameters.Length != 1)
                        { throw new CompilerException($"Wrong number of parameters passed to \"out\": requied {1} passed {0}", functionCall.TotalPosition(), CurrentFile); }

                        var outType = FindStatementType(functionCall.Parameters[0]);

                        if (!GetOutputWriter(outType, out var function))
                        { throw new CompilerException($"No function found with attribute \"{"StandardOutput"}\" that satisfies keyword-call {functionCall.ReadableID(FindStatementType)}", functionCall, CurrentFile); }

                        if (function.InstructionOffset == -1)
                        { throw new InternalException($"Function {function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                        GeneratedCode[item.CallInstructionIndex].Parameter = new DataItem(function.InstructionOffset - item.CallInstructionIndex);
                    }
                    else
                    { throw new NotImplementedException(); }
                }
                else
                { throw new NotImplementedException(); }

                parameters.Clear();
                compiledVariables.Clear();
            }

            if (OptimizeCode)
            {
                List<IFunctionThing> functionThings = new();
                functionThings.AddRange(this.CompiledFunctions);
                functionThings.AddRange(this.CompiledGeneralFunctions);
                BasicOptimizer.Optimize(this.GeneratedCode, functionThings.ToArray(), printCallback);
            }

            return new Result()
            {
                Code = GeneratedCode.ToArray(),
                DebugInfo = GeneratedDebugInfo.ToArray(),

                Functions = this.CompiledFunctions,
                Operators = this.CompiledOperators,
                GeneralFunctions = this.CompiledGeneralFunctions,
                Structs = this.CompiledStructs,
                Classes = this.CompiledClasses,

                Hints = this.Hints.ToArray(),
                Informations = this.Informations.ToArray(),
                Warnings = this.Warnings.ToArray(),
                Errors = this.Errors.ToArray(),
            };
        }

        public static Result Generate(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            Action<string, Output.LogType> printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
        {
            CodeGenerator codeGenerator = new();
            return codeGenerator.GenerateCode(
                compilerResult,
                settings,
                printCallback,
                level
                );
        }
    }
}