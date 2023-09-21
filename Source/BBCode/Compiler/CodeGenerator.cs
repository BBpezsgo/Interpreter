using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0051 // Remove unused private members

namespace ProgrammingLanguage.BBCode.Compiler
{
    using Bytecode;
    using Core;
    using Errors;
    using Parser;
    using Parser.Statement;

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

        public static CleanupItem operator +(CleanupItem a, CleanupItem b)
            => new(a.Size + b.Size, a.Count + b.Count);
    }

    public class DebugInfo
    {
        internal Position Position;
        internal int InstructionStart;
        internal int InstructionEnd;
    }

    public class CodeGenerator : CodeGeneratorBase
    {
        /*
         *      
         *      === Stack Structure ===
         *      
         *        -- ENTRY --
         *      
         *        ? ... pointers ... (external function cache) > ExternalFunctionsCache.Count
         *      
         *        ? ... variables ... (global variables)
         *        
         *        -- CALL --
         *      
         *   -5    return value
         *      
         *   -4    ? parameter "this"    \ ParametersSize()
         *   -3    ? ... parameters ...  /
         *      
         *   -2    saved code pointer
         *   -1    saved base pointer
         *   
         *   >> 
         *   
         *   0    return flag
         *   
         *   1    ? ... variables ... (locals)
         *   
         */

        #region Fields

        readonly Dictionary<string, ExternalFunctionBase> ExternalFunctions;
        readonly Dictionary<string, int> ExternalFunctionsCache;

        readonly Stack<CleanupItem> CleanupStack;
        IAmInContext<CompiledClass> CurrentContext;
        readonly List<CompiledParameter> CompiledParameters;
        readonly List<KeyValuePair<string, CompiledVariable>> CompiledVariables;

        readonly List<int> ReturnInstructions;
        readonly Stack<List<int>> BreakInstructions;

        readonly List<Instruction> GeneratedCode;

        readonly List<UndefinedFunctionOffset> UndefinedFunctionOffsets;
        readonly List<UndefinedOperatorFunctionOffset> UndefinedOperatorFunctionOffsets;
        readonly List<UndefinedGeneralFunctionOffset> UndefinedGeneralFunctionOffsets;

        readonly bool OptimizeCode;
        readonly bool AddCommentsToCode = true;
        readonly bool TrimUnreachableCode = true;
        readonly bool GenerateDebugInstructions = true;
        bool CanReturn;

        readonly List<Information> Informations;
        public readonly List<Hint> Hints;
        readonly List<DebugInfo> GeneratedDebugInfo;

        /// <summary>
        /// Used for keep track of local (after base pointer) tag count that are not variables.
        /// <br/>
        /// ie.:
        /// <br/>
        /// <c>Return Flag</c>
        /// </summary>
        readonly Stack<int> TagCount;

        #endregion

        public CodeGenerator(Compiler.CompilerSettings settings) : base()
        {
            this.ExternalFunctions = new Dictionary<string, ExternalFunctionBase>();
            this.GenerateDebugInstructions = settings.GenerateDebugInstructions;
            this.AddCommentsToCode = settings.GenerateComments;
            this.GeneratedCode = new List<Instruction>();
            this.ExternalFunctionsCache = new Dictionary<string, int>();
            this.OptimizeCode = !settings.DontOptimize;
            this.GeneratedDebugInfo = new List<DebugInfo>();
            this.CleanupStack = new Stack<CleanupItem>();
            this.ReturnInstructions = new List<int>();
            this.BreakInstructions = new Stack<List<int>>();
            this.UndefinedFunctionOffsets = new List<UndefinedFunctionOffset>();
            this.UndefinedOperatorFunctionOffsets = new List<UndefinedOperatorFunctionOffset>();
            this.UndefinedGeneralFunctionOffsets = new List<UndefinedGeneralFunctionOffset>();

            this.TagCount = new Stack<int>();

            this.CompiledVariables = new List<KeyValuePair<string, CompiledVariable>>();
            this.CompiledParameters = new List<CompiledParameter>();

            this.Informations = new List<Information>();
            this.Hints = new List<Hint>();
        }

        #region Helper Functions

        int CallRuntime(CompiledVariable address)
        {
            if (address.Type != Type.INT && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            if (address.IsStoredInHEAP)
            { AddInstruction(Opcode.HEAP_GET, AddressingMode.ABSOLUTE, address.MemoryAddress); }
            else
            { LoadFromStack(address.MemoryAddress, address.Type.Size, !address.IsGlobal); }

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(CompiledParameter address)
        {
            if (address.Type != Type.INT && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            int offset = GetDataAddress(address);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(StatementWithValue address)
        {
            if (FindStatementType(address) != Type.INT)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            GenerateCodeForStatement(address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, 0);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int Call(int absoluteAddress)
        {
            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, 0);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.ABSOLUTE, absoluteAddress - GeneratedCode.Count);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        void Return()
        {
            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RUNTIME, 0);
            AddInstruction(Opcode.SET_CODEPOINTER, AddressingMode.RUNTIME);
        }

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
            => CompiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, out CompiledParameter parameter)
            => CompiledParameters.TryGetValue(parameterName, out parameter);

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

            CompiledType instanceType = FindType(type);

            if (instanceType.IsStruct)
            {
                int size = 0;
                foreach (FieldDefinition field in instanceType.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (instanceType.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, BBCode.Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            if (instanceType.IsEnum)
            {
                if (instanceType.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{instanceType.Enum.Identifier.Content}\" initial value: enum has no members", instanceType.Enum.Identifier, instanceType.Enum.FilePath); }

                AddInstruction(Opcode.PUSH_VALUE, instanceType.Enum.Members[0].Value, tag);
                return 1;
            }

            if (instanceType.IsFunction)
            {
                AddInstruction(Opcode.PUSH_VALUE, BBCode.Utils.NULL_POINTER, tag);
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
                AddInstruction(Opcode.PUSH_VALUE, BBCode.Utils.NULL_POINTER, $"(pointer) {tag}");
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
                AddInstruction(Opcode.PUSH_VALUE, BBCode.Utils.NULL_POINTER, $"(pointer) {tag}");
                afterValue?.Invoke(0);
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
            afterValue?.Invoke(0);
            return 1;
        }

        int ParametersSize()
        {
            int sum = 0;

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                sum += CompiledParameters[i].Type.SizeOnStack;
            }

            return sum;
        }
        int ParametersSize(int beforeThis)
        {
            int sum = 0;

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                if (CompiledParameters[i].Index < beforeThis) continue;

                sum += CompiledParameters[i].Type.SizeOnStack;
            }

            return sum;
        }

        #endregion

        #region AddInstruction()

        void AddInstruction(Instruction instruction)
        {
            if (instruction.opcode == Opcode.DEBUG_SET_TAG && !GenerateDebugInstructions)
            { return; }

            GeneratedCode.Add(instruction);
        }
        void AddInstruction(Opcode opcode) => AddInstruction(new Instruction(opcode));
        void AddInstruction(Opcode opcode, DataItem param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, string tag = null) => AddInstruction(new Instruction(opcode) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, new DataItem(param0)) { tag = tag ?? string.Empty });

        void AddInstruction(Opcode opcode, AddressingMode addressingMode) => AddInstruction(new Instruction(opcode, addressingMode));
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, new DataItem(param0)) { tag = tag ?? string.Empty });

        void AddComment(string comment)
        {
            if (!AddCommentsToCode) return;
            AddInstruction(Opcode.COMMENT, comment);
        }
        void AddCommentForce(string comment)
        {
            AddInstruction(Opcode.COMMENT, comment);
        }
        #endregion

        #region GenerateCodeForStatement

        void GenerateCodeForStatement(VariableDeclaretion newVariable)
        {
            newVariable.VariableName.AnalysedType = TokenAnalysedType.VariableName;

            if (!GetVariable(newVariable.VariableName.Content, out CompiledVariable compiledVariable))
            { throw new CompilerException($"Variable \"{newVariable.VariableName.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", newVariable.VariableName, CurrentFile); }

            if (compiledVariable.Type.IsClass)
            { newVariable.Type.Identifier.AnalysedType = TokenAnalysedType.Class; }
            else if (compiledVariable.Type.IsStruct)
            { newVariable.Type.Identifier.AnalysedType = TokenAnalysedType.Struct; }
            else if (compiledVariable.Type.IsEnum)
            { newVariable.Type.Identifier.AnalysedType = TokenAnalysedType.Enum; }

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
                    int offset = ReturnValueOffset;

                    for (int i = 0; i < returnValueType.SizeOnStack; i++)
                    { AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset - i); }
                }

                // keywordCall.Identifier = keywordCall.Identifier.Statement("return", "void", new string[] { "p" }, new string[] { "any" });

                AddComment(" .:");

                if (CanReturn)
                {
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(true), "RETURN_FLAG");
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, ReturnFlagOffset);
                }

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

                GenerateCodeForStatement(throwValue);
                AddInstruction(Opcode.THROW);

                return;
            }

            if (keywordCall.FunctionName == "break")
            {
                if (BreakInstructions.Count == 0)
                { throw new CompilerException($"The keyword \"break\" does not avaiable in the current context", keywordCall.Identifier, CurrentFile); }

                BreakInstructions.Last.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                AddComment("}");

                return;
            }

            if (keywordCall.FunctionName == "sizeof")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": requied {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

                StatementWithValue param0 = keywordCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                AddInstruction(Opcode.PUSH_VALUE, param0Type.SizeOnStack, $"sizeof({param0Type.Name})");

                return;
            }

            if (keywordCall.FunctionName == "delete")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"delete\": requied {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

                CompiledType paramType = FindStatementType(keywordCall.Parameters[0]);

                if (paramType == Type.INT)
                {
                    GenerateCodeForStatement(keywordCall.Parameters[0], new CompiledType(Type.INT));
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    return;
                }

                if (paramType.IsClass)
                {
                    if (!GetGeneralFunction(paramType.Class, FindStatementTypes(keywordCall.Parameters), FunctionNames.Destructor, out var destructor))
                    {
                        if (!GetGeneralFunctionTemplate(paramType.Class, FindStatementTypes(keywordCall.Parameters), FunctionNames.Destructor, out var destructorTemplate))
                        {
                            GenerateCodeForStatement(keywordCall.Parameters[0], new CompiledType(Type.INT));
                            AddInstruction(Opcode.HEAP_DEALLOC);

                            return;
                        }
                        destructorTemplate = AddCompilable(destructorTemplate);
                        destructor = destructorTemplate.Function;
                    }

                    if (!destructor.CanUse(CurrentFile))
                    {
                        Errors.Add(new Error($"Destructor for type '{paramType.Class.Name.Content}' function cannot be called due to its protection level", keywordCall.Identifier, CurrentFile));
                        AddComment("}");
                        return;
                    }

                    AddComment(" Param0:");
                    GenerateCodeForStatement(keywordCall.Parameters[0], paramType);
                    AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");

                    AddComment(" .:");

                    int jumpInstruction = Call(destructor.InstructionOffset);

                    if (destructor.InstructionOffset == -1)
                    { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(jumpInstruction, keywordCall, destructor, CurrentFile)); }

                    AddComment(" Clear Param:");

                    AddInstruction(Opcode.POP_VALUE);

                    AddComment("}");

                    return;
                }

                Warnings.Add(new Warning($"The 'delete' keyword-function is only working on type class or int so I skip this shit", keywordCall.Parameters[0], CurrentFile));
                return;
            }

            if (keywordCall.FunctionName == "clone")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"clone\": requied {1} passed {0}", keywordCall, CurrentFile); }

                var paramType = FindStatementType(keywordCall.Parameters[0]);

                if (!paramType.IsClass)
                {
                    Warnings.Add(new Warning($"The 'clone' function is only working on type class so I skip this shit", keywordCall.Parameters[0], CurrentFile));
                    return;
                }

                // keywordCall.Identifier = keywordCall.Identifier.BuiltinFunction("clone", "void", new string[] { "object" }, new string[] { "any" });

                if (!GetGeneralFunction(paramType.Class, FunctionNames.Cloner, out var cloner))
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
                GenerateCodeForStatement(keywordCall.Parameters[0], paramType);
                AddInstruction(Opcode.DEBUG_SET_TAG, $"param.{cloner.Parameters[0].Identifier}");

                AddComment(" .:");

                int jumpInstruction = Call(cloner.InstructionOffset);

                if (cloner.InstructionOffset == -1)
                { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(jumpInstruction, keywordCall, cloner, CurrentFile)); }

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
                { throw new CompilerException($"Wrong number of parameters passed to \"out\": requied {1} passed {0}", keywordCall, CurrentFile); }

                var outType = FindStatementType(keywordCall.Parameters[0]);

                if (!GetOutputWriter(outType, out var function))
                { throw new CompilerException($"No function found with attribute \"{"StandardOutput"}\" that satisfies keyword-call {keywordCall.ReadableID(FindStatementType)}", keywordCall, CurrentFile); }

                if (function.IsExternal)
                {
                    if (!ExternalFunctions.TryGetValue(function.ExternalFunctionName, out ExternalFunctionBase externalFunction))
                    {
                        Errors.Add(new Error($"External function \"{function.ExternalFunctionName}\" not found", keywordCall.Identifier, CurrentFile));
                        AddComment("}");
                        return;
                    }

                    AddComment(" Function Name:");
                    if (ExternalFunctionsCache.TryGetValue(function.ExternalFunctionName, out int cacheAddress))
                    {
                        if (function.ExternalFunctionName.Length == 0)
                        { throw new CompilerException($"External function with length of zero", (FunctionDefinition.Attribute)function.Attributes.Get("External"), function.FilePath); }

                        AddComment($" Param {0}:");
                        GenerateCodeForStatement(keywordCall.Parameters[0]);
                        AddInstruction(Opcode.PUSH_VALUE, function.ExternalFunctionName.Length, "ID Length");

                        AddComment($" Load Function Name String Pointer (Cache):");
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress);

                        AddComment(" .:");
                        AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

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
                        GenerateCodeForLiteralString(function.ExternalFunctionName);

                        int offset = -1;

                        AddComment($" Param {0}:");
                        GenerateCodeForStatement(keywordCall.Parameters[0]);
                        offset--;

                        AddInstruction(Opcode.PUSH_VALUE, 0, "ID Length");
                        offset--;

                        AddComment($" Load Function Name String Pointer:");
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, offset);

                        AddComment(" .:");
                        AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

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

                int jumpInstruction = Call(function.InstructionOffset);

                if (function.InstructionOffset == -1)
                { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(jumpInstruction, new FunctionCall(null, keywordCall.Identifier, null, keywordCall.Parameters, null), function, CurrentFile)); }

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
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": requied {1} passed {functionCall.Parameters.Length}", functionCall, CurrentFile); }

                StatementWithValue param0 = functionCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                AddInstruction(Opcode.PUSH_VALUE, param0Type.SizeOnStack, $"sizeof({param0Type.Name})");

                return;
            }

            if (functionCall.FunctionName == "Alloc")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"Alloc\": requied {1} passed {functionCall.Parameters.Length}", functionCall, CurrentFile); }

                GenerateCodeForStatement(functionCall.Parameters[0], new CompiledType(Type.INT));

                AddInstruction(Opcode.HEAP_ALLOC, "(pointer)");

                return;
            }

            if (GetVariable(functionCall.Identifier.Content, out CompiledVariable compiledVariable))
            {
                if (!compiledVariable.Type.IsFunction)
                { throw new CompilerException($"Variable \"{compiledVariable.VariableName.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

                GenerateCodeForFunctionCall_Variable(functionCall, compiledVariable);
                return;
            }

            if (GetParameter(functionCall.Identifier.Content, out CompiledParameter compiledParameter))
            {
                if (!compiledParameter.Type.IsFunction)
                { throw new CompilerException($"Variable \"{compiledParameter.Identifier.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

                GenerateCodeForFunctionCall_Variable(functionCall, compiledParameter);
                return;
            }

            if (!GetFunction(functionCall, out CompiledFunction compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompileableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compilableFunction = AddCompilable(compilableFunction);
                compiledFunction = compilableFunction.Function;
            }

            GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);
        }

        void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
        {
            // functionCall.Identifier = functionCall.Identifier.Function(compiledFunction);

            if (!compiledFunction.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The {compiledFunction.ReadableID()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
                return;
            }

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ReadableID()}: requied {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {(compiledFunction.IsMethod ? "method" : "function")} \"{functionCall.FunctionName}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

            if (compiledFunction.IsMacro)
            {
                Warnings.Add(new Warning($"I can not inline macros becouse of lack of intelligence so I will treat this macro as a normal function.", functionCall, CurrentFile));
                // InlineMacro(compiledFunction, functionCall.MethodParameters, functionCall.SaveValue);
                // return;
            }

            AddComment($"Call {compiledFunction.ReadableID()} {{");

            if (compiledFunction.IsExternal)
            {
                if (!ExternalFunctions.TryGetValue(compiledFunction.ExternalFunctionName, out var externalFunction))
                {
                    Errors.Add(new Error($"External function \"{compiledFunction.ExternalFunctionName}\" not found", functionCall.Identifier, CurrentFile));
                    AddComment("}");
                    return;
                }

                AddComment(" Function Name:");
                if (ExternalFunctionsCache.TryGetValue(compiledFunction.ExternalFunctionName, out int cacheAddress))
                {
                    if (compiledFunction.ExternalFunctionName.Length == 0)
                    { throw new CompilerException($"External function with length of zero", (FunctionDefinition.Attribute)compiledFunction.Attributes.Get("External"), compiledFunction.FilePath); }

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

                    AddComment($" Load Function Name String Pointer (Cache):");

                    AddInstruction(Opcode.PUSH_VALUE, compiledFunction.ExternalFunctionName.Length, "ID Length");

                    AddInstruction(Opcode.PUSH_VALUE, cacheAddress);
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RUNTIME);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

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
                    GenerateCodeForLiteralString(compiledFunction.ExternalFunctionName);

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
                    AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

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
                string paramName = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i].Identifier.Content;
                CompiledType paramType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

                AddComment($" Param {i}:");
                GenerateCodeForStatement(param, paramType);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + paramName);

                paramsSize += paramType.SizeOnStack;
            }

            AddComment(" .:");

            int jumpInstruction = Call(compiledFunction.InstructionOffset);

            if (compiledFunction.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(jumpInstruction, functionCall, compiledFunction, CurrentFile)); }

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
            FunctionType functionType = compiledVariable.Type.Function;

            AddComment($"Call {compiledVariable.Type.Function} {{");

            functionCall.Identifier.AnalysedType = TokenAnalysedType.VariableName;

            if (functionCall.MethodParameters.Length != functionType.Parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to {functionType}: requied {functionType.Parameters.Length} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            int returnValueSize = 0;
            if (functionType.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(functionType.ReturnType, "returnvalue");
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
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                CompiledType definedParameterType = functionType.Parameters[i];

                if (passedParameterType != definedParameterType)
                { throw new CompilerException($"This should be a {definedParameterType} not a {passedParameterType} (parameter {i + 1})", passedParameter, CurrentFile); }

                AddComment($" Param {i}:");
                GenerateCodeForStatement(passedParameter, definedParameterType);
                AddInstruction(Opcode.DEBUG_SET_TAG, $"param.{i}");

                paramsSize += definedParameterType.SizeOnStack;
            }

            AddComment(" .:");

            CallRuntime(compiledVariable);

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

            if (functionType.ReturnSomething && !functionCall.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }
        void GenerateCodeForFunctionCall_Variable(FunctionCall functionCall, CompiledParameter compiledParameter)
        {
            FunctionType functionType = compiledParameter.Type.Function;

            AddComment($"Call {compiledParameter.Type.Function} {{");

            functionCall.Identifier.AnalysedType = TokenAnalysedType.VariableName;

            if (functionCall.MethodParameters.Length != functionType.Parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to {functionType}: requied {functionType.Parameters.Length} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            int returnValueSize = 0;
            if (functionType.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(functionType.ReturnType, "returnvalue");
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
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                CompiledType definedParameterType = functionType.Parameters[i];

                if (passedParameterType != definedParameterType)
                { throw new CompilerException($"This should be a {definedParameterType} not a {passedParameterType} (parameter {i + 1})", passedParameter, CurrentFile); }

                AddComment($" Param {i}:");
                GenerateCodeForStatement(passedParameter, definedParameterType);
                AddInstruction(Opcode.DEBUG_SET_TAG, $"param.{i}");

                paramsSize += definedParameterType.SizeOnStack;
            }

            AddComment(" .:");

            CallRuntime(compiledParameter);

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

            if (functionType.ReturnSomething && !functionCall.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }

        void GenerateCodeForStatement(OperatorCall @operator)
        {
            if (OptimizeCode && TryCompute(@operator, out DataItem predictedValue))
            {
                AddInstruction(Opcode.PUSH_VALUE, predictedValue);
                Informations.Add(new Information($"Predicted value: {predictedValue}", @operator, CurrentFile));
                return;
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
                { throw new CompilerException($"Wrong number of parameters passed to operator {operatorDefinition.ReadableID()}: requied {operatorDefinition.ParameterCount} passed {@operator.ParameterCount}", @operator, CurrentFile); }

                if (operatorDefinition.IsExternal)
                {
                    if (!ExternalFunctions.TryGetValue(operatorDefinition.ExternalFunctionName, out ExternalFunctionBase externalFunction))
                    {
                        Errors.Add(new Error($"External function \"{operatorDefinition.ExternalFunctionName}\" not found", @operator.Operator, CurrentFile));
                        AddComment("}");
                        return;
                    }

                    AddComment(" Function Name:");
                    if (ExternalFunctionsCache.TryGetValue(operatorDefinition.ExternalFunctionName, out int cacheAddress))
                    { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress); }
                    else
                    { GenerateCodeForLiteralString(operatorDefinition.ExternalFunctionName); }

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
                    AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

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
                    StatementWithValue passedParameter = @operator.Parameters[i];
                    CompiledType passedParameterType = FindStatementType(passedParameter);
                    ParameterDefinition definedParameter = operatorDefinition.Parameters[i];
                    CompiledType definedParameterType = operatorDefinition.ParameterTypes[i];

                    if (passedParameterType != definedParameterType)
                    { }

                    AddComment($" Param {i}:");
                    GenerateCodeForStatement(passedParameter, definedParameterType);
                    AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParameter.Identifier);

                    paramsSize += passedParameterType.SizeOnStack;
                }

                AddComment(" .:");

                int jumpInstruction = Call(operatorDefinition.InstructionOffset);

                if (operatorDefinition.InstructionOffset == -1)
                { UndefinedOperatorFunctionOffsets.Add(new UndefinedOperatorFunctionOffset(jumpInstruction, @operator, operatorDefinition, CurrentFile)); }

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

                int jumpInstruction = -1;

                GenerateCodeForStatement(@operator.Left);

                if (opcode == Opcode.LOGIC_AND)
                {
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1);
                    jumpInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE);
                }
                else if (opcode == Opcode.LOGIC_OR)
                {
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1);
                    AddInstruction(Opcode.LOGIC_NOT);
                    jumpInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE);
                }

                if (@operator.Right != null) GenerateCodeForStatement(@operator.Right);
                AddInstruction(opcode);

                if (jumpInstruction != -1)
                { GeneratedCode[jumpInstruction].ParameterInt = GeneratedCode.Count - jumpInstruction; }
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
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(int.Parse(literal.Value)));
                    break;
                case LiteralType.FLOAT:
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(float.Parse(literal.Value.TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                case LiteralType.STRING:
                    GenerateCodeForLiteralString(literal);
                    break;
                case LiteralType.BOOLEAN:
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem((bool.Parse(literal.Value) ? 1 : 0)));
                    break;
                case LiteralType.CHAR:
                    if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", CurrentFile);
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal.Value[0]));
                    break;
                default: throw new ImpossibleException();
            }
        }

        void GenerateCodeForLiteralString(BBCode.Parser.Statement.Literal literal)
            => GenerateCodeForLiteralString(literal.Value);
        void GenerateCodeForLiteralString(string literal)
        {
            AddComment($"Create String \"{literal}\" {{");

            AddComment("Allocate String object {");

            AddInstruction(Opcode.PUSH_VALUE, 1 + literal.Length, $"sizeof(String) (on heap)");
            AddInstruction(Opcode.HEAP_ALLOC, $"(pointer String)");

            AddComment("}");

            AddComment("Set String.length {");
            // Set String.length
            {
                AddInstruction(Opcode.PUSH_VALUE, literal.Length);
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");

            AddComment("Set string data {");
            for (int i = 0; i < literal.Length; i++)
            {
                // Prepare value
                AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal[i]));

                // Calculate pointer
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
                AddInstruction(Opcode.PUSH_VALUE, 1 + i);
                AddInstruction(Opcode.MATH_ADD);

                // Set value
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");

            AddComment("}");
        }

        void GenerateCodeForStatement(Identifier variable, CompiledType expectedType = null)
        {
            if (GetParameter(variable.Content, out CompiledParameter param))
            {
                int offset = GetDataAddress(param);
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);

                return;
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            {
                if (val.IsStoredInHEAP)
                { AddInstruction(Opcode.LOAD_VALUE, val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, val.MemoryAddress); }
                else
                { LoadFromStack(val.MemoryAddress, val.Type.Size, !val.IsGlobal); }

                return;
            }

            if (GetFunction(variable.Name, expectedType, out CompiledFunction compiledFunction))
            {
                if (compiledFunction.InstructionOffset == -1)
                { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(GeneratedCode.Count, variable, compiledFunction, CurrentFile)); }

                AddInstruction(Opcode.PUSH_VALUE, compiledFunction.InstructionOffset, $"(function) {compiledFunction.ReadableID()}");

                variable.Name.AnalysedType = TokenAnalysedType.FunctionName;

                return;
            }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
        }
        void GenerateCodeForStatement(AddressGetter addressGetter)
        {
            if (addressGetter.PrevStatement is Identifier identifier)
            {
                CompiledType type = FindStatementType(identifier);

                if (!type.InHEAP)
                { throw new CompilerException($"Variable \"{identifier}\" (type of {type}) is stored on the stack", addressGetter, CurrentFile); }

                GenerateCodeForStatement(identifier);
                return;
            }
            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(Pointer memoryAddressFinder)
        {
            GenerateCodeForStatement(memoryAddressFinder.PrevStatement);
            // TODO: stack getter
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }
        void GenerateCodeForStatement(WhileLoop whileLoop)
        {
            bool conditionIsComputed = TryCompute(whileLoop.Condition, out DataItem computedCondition);
            if (conditionIsComputed && computedCondition.IsFalsy() && TrimUnreachableCode)
            {
                AddComment("Unreachable code not compiled");
                Informations.Add(new Information($"Unreachable code not compiled", whileLoop.Block, CurrentFile));
                return;
            }

            AddComment("while (...) {");

            OnScopeEnter(whileLoop.Block);

            AddComment("Condition");
            int conditionOffset = GeneratedCode.Count;
            GenerateCodeForStatement(whileLoop.Condition);

            int conditionJumpOffset = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

            BreakInstructions.Push(new List<int>());

            GenerateCodeForStatement(whileLoop.Block, true);

            AddComment("Jump Back");
            AddInstruction(Opcode.JUMP_BY, conditionOffset - GeneratedCode.Count);

            FinishJumpInstructions(BreakInstructions.Last);

            GeneratedCode[conditionJumpOffset].ParameterInt = GeneratedCode.Count - conditionJumpOffset;

            OnScopeExit();

            AddComment("}");

            if (conditionIsComputed)
            {
                if (computedCondition.IsFalsy())
                { Warnings.Add(new Warning($"Bruh", whileLoop.Keyword, CurrentFile)); }
                else if (BreakInstructions.Last.Count == 0)
                { Warnings.Add(new Warning($"Potential infinity loop", whileLoop.Keyword, CurrentFile)); }
            }

            BreakInstructions.Pop();
        }
        void GenerateCodeForStatement(ForLoop forLoop)
        {
            AddComment("for (...) {");

            OnScopeEnter(forLoop.Block);

            CleanupStack[^1] = CleanupStack[^1] + GenerateCodeForVariable(forLoop.VariableDeclaration, false);

            AddComment("FOR Declaration");
            // Index variable
            GenerateCodeForStatement(forLoop.VariableDeclaration);

            AddComment("FOR Condition");
            // Index condition
            int conditionOffsetFor = GeneratedCode.Count;
            GenerateCodeForStatement(forLoop.Condition);
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffsetFor = GeneratedCode.Count - 1;

            BreakInstructions.Push(new List<int>());

            GenerateCodeForStatement(forLoop.Block, true);

            AddComment("FOR Expression");
            // Index expression
            GenerateCodeForStatement(forLoop.Expression);

            AddComment("Jump back");
            AddInstruction(Opcode.JUMP_BY, conditionOffsetFor - GeneratedCode.Count);
            GeneratedCode[conditionJumpOffsetFor].ParameterInt = GeneratedCode.Count - conditionJumpOffsetFor;

            FinishJumpInstructions(BreakInstructions.Pop());

            OnScopeExit();

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

                    GenerateCodeForStatement(partIf.Block);

                    AddComment("IF Jump to End");
                    jumpOutInstructions.Add(GeneratedCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddComment("}");

                    GeneratedCode[jumpNextInstruction].ParameterInt = GeneratedCode.Count - jumpNextInstruction;
                }
                else if (ifSegment is ElseIfBranch partElseif)
                {
                    AddComment("elseif (...) {");

                    AddComment("ELSEIF Condition");
                    GenerateCodeForStatement(partElseif.Condition);
                    AddComment("ELSEIF Jump to Next");
                    int jumpNextInstruction = GeneratedCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    GenerateCodeForStatement(partElseif.Block);

                    AddComment("IF Jump to End");
                    jumpOutInstructions.Add(GeneratedCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddComment("}");

                    GeneratedCode[jumpNextInstruction].ParameterInt = GeneratedCode.Count - jumpNextInstruction;
                }
                else if (ifSegment is ElseBranch partElse)
                {
                    AddComment("else {");

                    GenerateCodeForStatement(partElse.Block);

                    AddComment("}");
                }
            }

            foreach (var item in jumpOutInstructions)
            {
                GeneratedCode[item].ParameterInt = GeneratedCode.Count - item;
            }
        }
        void GenerateCodeForStatement(NewInstance newObject)
        {
            AddComment($"new {newObject.TypeName} {{");

            CompiledType instanceType = FindType(newObject.TypeName);

            if (instanceType.IsStruct)
            {
                // newObject.TypeName = newObject.TypeName.Struct(instanceType.Struct);
                instanceType.Struct.References?.Add(new DefinitionReference(newObject.TypeName.Identifier, CurrentFile));

                throw new NotImplementedException();
            }
            else if (instanceType.IsClass)
            {
                // newObject.TypeName = newObject.TypeName.Class(instanceType.Class);
                instanceType.Class.References?.Add(new DefinitionReference(newObject.TypeName.Identifier, CurrentFile));

                if (instanceType.Class.TemplateInfo != null)
                {
                    if (newObject.TypeName.GenericTypes.Count == 0)
                    { throw new CompilerException($"No type arguments specified for class instance \"{instanceType}\"", newObject.TypeName, CurrentFile); }

                    if (instanceType.Class.TemplateInfo.TypeParameters.Length != newObject.TypeName.GenericTypes.Count)
                    { throw new CompilerException($"Wrong number of type arguments specified for class instance \"{instanceType}\": require {instanceType.Class.TemplateInfo.TypeParameters.Length} specified {newObject.TypeName.GenericTypes.Count}", newObject.TypeName, CurrentFile); }
                }
                else
                {
                    if (newObject.TypeName.GenericTypes.Count > 0)
                    { throw new CompilerException($"You should not specify type arguments for class instance \"{instanceType}\"", newObject.TypeName, CurrentFile); }
                }

                CompiledType[] genericParameters = newObject.TypeName.GenericTypes.Select(v => new CompiledType(v, FindType)).ToArray();

                instanceType.Class.AddTypeArguments(genericParameters);

                AddInstruction(Opcode.PUSH_VALUE, instanceType.Class.Size, $"sizeof({instanceType.Class.Name.Content}) (on heap)");
                AddInstruction(Opcode.HEAP_ALLOC, $"(pointer {instanceType.Class.Name.Content})");
                int currentOffset = 0;
                for (int fieldIndex = 0; fieldIndex < instanceType.Class.Fields.Length; fieldIndex++)
                {
                    CompiledField field = instanceType.Class.Fields[fieldIndex];
                    AddComment($"Create Field '{field.Identifier.Content}' ({fieldIndex}) {{");

                    CompiledType fieldType = field.Type;

                    if (fieldType.IsGeneric && !instanceType.Class.CurrentTypeArguments.TryGetValue(fieldType.Name, out fieldType))
                    { throw new CompilerException($"Type argument \"{fieldType.Name}\" not found", field, instanceType.Class.FilePath); }

                    GenerateInitialValue(fieldType, j =>
                    {
                        AddComment($"Save Chunk {j}:");
                        AddInstruction(Opcode.PUSH_VALUE, currentOffset);
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -3);
                        AddInstruction(Opcode.MATH_ADD);
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                        currentOffset++;
                    }, $"{instanceType.Class.Name.Content}.{field.Identifier.Content}");
                    AddComment("}");
                }

                instanceType.Class.ClearTypeArguments();
            }
            else
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", newObject.TypeName, CurrentFile); }
            AddComment("}");
        }
        void GenerateCodeForStatement(ConstructorCall constructorCall)
        {
            AddComment($"new {constructorCall.TypeName}(...): {{");

            var instanceType = FindType(constructorCall.TypeName);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            // constructorCall.TypeName = constructorCall.TypeName.Class(instanceType.Class);
            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.TypeName.Identifier, CurrentFile));

            if (!GetClass(constructorCall, out var @class))
            { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

            if (!GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), FunctionNames.Constructor, out CompiledGeneralFunction constructor))
            {
                if (!GetConstructorTemplate(@class, constructorCall, out var compilableGeneralFunction))
                {
                    throw new CompilerException($"Function {constructorCall.ReadableID(FindStatementType)} not found", constructorCall.Keyword, CurrentFile);
                }
                else
                {
                    compilableGeneralFunction = AddCompilable(compilableGeneralFunction);
                    constructor = compilableGeneralFunction.Function;
                }
            }

            if (!constructor.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The \"{constructorCall.TypeName}\" constructor cannot be called due to its protection level", constructorCall.Keyword, CurrentFile));
                AddComment("}");
                return;
            }

            if (constructorCall.Parameters.Length != constructor.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to \"{constructorCall.TypeName}\" constructor: requied {constructor.ParameterCount} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

            int returnValueSize = 0;
            if (constructor.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(constructor.Type, "returnvalue");
            }

            int paramsSize = 0;

            for (int i = 0; i < constructorCall.Parameters.Length; i++)
            {
                Statement passedParameter = constructorCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(constructorCall.Parameters[i]);

                CompiledType parameterType = constructor.ParameterTypes[constructor.IsMethod ? (i + 1) : i];
                ParameterDefinition parameterDefinition = constructor.Parameters[constructor.IsMethod ? (i + 1) : i];

                if (parameterType != passedParameterType)
                { }

                AddComment($" Param {i}:");
                GenerateCodeForStatement(passedParameter);
                AddInstruction(Opcode.DEBUG_SET_TAG, $"param.{parameterDefinition.Identifier}");

                paramsSize += parameterType.SizeOnStack;
            }

            AddComment(" .:");

            int jumpInstruction = Call(constructor.InstructionOffset);

            if (constructor.InstructionOffset == -1)
            { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(jumpInstruction, constructorCall, constructor, CurrentFile)); }

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
                if (!GetField(field, out var compiledField))
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
                        default: throw new ImpossibleException();
                    }
                }
            }

            if (IsItInHeap(field))
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

            if (!GetIndexGetter(prevType, out CompiledFunction indexer))
            {
                if (!GetIndexGetterTemplate(prevType, out CompileableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index getter for class \"{prevType.Class.Name}\" not found", index, CurrentFile); }

                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }

            GenerateCodeForFunctionCall_Function(new FunctionCall(
                    index.PrevStatement,
                    Token.CreateAnonymous(FunctionNames.IndexerGet),
                    index.BracketLeft,
                    new StatementWithValue[]
                    {
                        index.Expression,
                    },
                    index.BracketRight
                ), indexer);

            return;
        }
        void GenerateCodeForStatement(LiteralList listValue)
        { throw new NotImplementedException(); }
        void GenerateCodeForStatement(TypeCast @as)
        {
            CompiledType targetType = new(@as.Type, FindType);

            GenerateCodeForStatement(@as.PrevStatement, targetType);

            CompiledType type = FindStatementType(@as.PrevStatement, targetType);

            if (!targetType.IsFunction && type == targetType)
            {
                Hints.Add(new Hint($"Redundant type conversion", @as.Keyword, CurrentFile));
                return;
            }

            if (type.IsBuiltin && targetType.IsBuiltin)
            {
                AddInstruction(Opcode.PUSH_VALUE, new DataItem((byte)targetType.BuiltinType.Convert()), $"typecast target type");
                AddInstruction(Opcode.TYPE_SET);
            }
        }

        /// <param name="silent">
        /// If set to <see langword="true"/> then it will <b>ONLY</b> generate the statements and does not
        /// generate variables or something like that.
        /// </param>
        void GenerateCodeForStatement(Block block, bool silent = false)
        {
            if (silent)
            {
                AddComment("Statements {");
                for (int i = 0; i < block.Statements.Count; i++)
                { GenerateCodeForStatement(block.Statements[i]); }
                AddComment("}");

                return;
            }

            OnScopeEnter(block);

            AddComment("Statements {");
            for (int i = 0; i < block.Statements.Count; i++)
            { GenerateCodeForStatement(block.Statements[i]); }
            AddComment("}");

            OnScopeExit();
        }

        void GenerateCodeForStatement(Statement statement, CompiledType expectedType = null)
        {
            DebugInfo debugInfo = new()
            {
                InstructionStart = GeneratedCode.Count,
                InstructionEnd = GeneratedCode.Count,
            };

            if (statement is LiteralList listValue)
            { GenerateCodeForStatement(listValue); }
            else if (statement is VariableDeclaretion newVariable)
            { GenerateCodeForStatement(newVariable); }
            else if (statement is FunctionCall functionCall)
            { GenerateCodeForStatement(functionCall); }
            else if (statement is KeywordCall keywordCall)
            { GenerateCodeForStatement(keywordCall); }
            else if (statement is OperatorCall @operator)
            { GenerateCodeForStatement(@operator); }
            else if (statement is AnyAssignment setter)
            { GenerateCodeForStatement(setter.ToAssignment()); }
            else if (statement is BBCode.Parser.Statement.Literal literal)
            { GenerateCodeForStatement(literal); }
            else if (statement is Identifier variable)
            { GenerateCodeForStatement(variable, expectedType); }
            else if (statement is AddressGetter memoryAddressGetter)
            { GenerateCodeForStatement(memoryAddressGetter); }
            else if (statement is Pointer memoryAddressFinder)
            { GenerateCodeForStatement(memoryAddressFinder); }
            else if (statement is WhileLoop whileLoop)
            { GenerateCodeForStatement(whileLoop); }
            else if (statement is ForLoop forLoop)
            { GenerateCodeForStatement(forLoop); }
            else if (statement is IfContainer @if)
            { GenerateCodeForStatement(@if); }
            else if (statement is NewInstance newStruct)
            { GenerateCodeForStatement(newStruct); }
            else if (statement is ConstructorCall constructorCall)
            { GenerateCodeForStatement(constructorCall); }
            else if (statement is IndexCall indexStatement)
            { GenerateCodeForStatement(indexStatement); }
            else if (statement is Field field)
            { GenerateCodeForStatement(field); }
            else if (statement is TypeCast @as)
            { GenerateCodeForStatement(@as); }
            else
            { throw new InternalException($"Unimplemented statement {statement.GetType().Name}"); }

            debugInfo.InstructionEnd = GeneratedCode.Count - 1;
            debugInfo.Position = statement.GetPosition();
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
                default: throw new ImpossibleException();
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
                default: throw new ImpossibleException();
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
            if (addComments) AddComment("Variables {");
            int count = 0;
            int variableSizeSum = 0;
            foreach (var s in block.Statements)
            {
                var v = GenerateCodeForVariable(s, isGlobal);
                variableSizeSum += v.Size;
                count += v.Count;
            }
            if (addComments) AddComment("}");
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
            { CompiledVariables.Remove(CompiledVariables[^1].Key); }
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

            if (GetParameter(statementToSet.Content, out CompiledParameter parameter))
            {
                // statementToSet.VariableName = statementToSet.VariableName.Variable(parameter);

                if (parameter.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{parameter.Type.Name}\" type parameter.", value, CurrentFile); }

                GenerateCodeForStatement(value);
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
            }
            else if (GetVariable(statementToSet.Content, out CompiledVariable variable))
            {
                // statementToSet.VariableName = statementToSet.VariableName.Variable(variable);

                if (variable.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{variable.Type.Name}\" type variable.", value, CurrentFile); }

                GenerateCodeForStatement(value);
                AddInstruction(Opcode.STORE_VALUE, variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
            }
            else
            {
                throw new CompilerException($"Variable \"{statementToSet.Content}\" not found", statementToSet, CurrentFile);
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

            // if (prevType.IsStruct)
            // { statementToSet.FieldName = statementToSet.FieldName.Field(prevType.Struct, statementToSet, type); }
            // else if (prevType.IsClass)
            // { statementToSet.FieldName = statementToSet.FieldName.Field(prevType.Class, statementToSet, type); }

            if (type != valueType)
            { throw new CompilerException($"Can not set a \"{valueType}\" type value to the \"{type}\" type field.", value, CurrentFile); }

            if (prevType.IsClass)
            { prevType.Class.AddTypeArguments(prevType.TypeParameters); }

            GenerateCodeForStatement(value);

            bool _inHeap = IsItInHeap(statementToSet);

            if (_inHeap)
            {
                int offset = GetFieldOffset(statementToSet);
                int pointerOffset = GetBaseAddress(statementToSet, out AddressingMode addressingMode);
                GenerateCodeForValueSetter(pointerOffset, offset, addressingMode);

                if (prevType.IsClass)
                { prevType.Class.ClearTypeArguments(); }

                return;
            }
            else
            {
                int offset = GetDataAddress(statementToSet, out AddressingMode addressingMode);
                GenerateCodeForValueSetter(offset, addressingMode);

                if (prevType.IsClass)
                { prevType.Class.ClearTypeArguments(); }

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

            if (!GetIndexSetter(prevType, valueType, out CompiledFunction indexer))
            {
                if (!GetIndexSetterTemplate(prevType, valueType, out CompileableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index setter for class \"{prevType.Class.Name}\" not found", statementToSet, CurrentFile); }

                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }

            GenerateCodeForFunctionCall_Function(new FunctionCall(
                    statementToSet.PrevStatement,
                    Token.CreateAnonymous(FunctionNames.IndexerSet),
                    statementToSet.BracketLeft,
                    new StatementWithValue[]
                    {
                        statementToSet.Expression,
                        value,
                    },
                    statementToSet.BracketRight
                ), indexer);

            return;
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

            // statementToSet.VariableName = statementToSet.VariableName.Variable(variable);

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

            throw new CompilerException($"Can not set a {valueType} type value to the {destination} type variable.", value, CurrentFile);
        }

        #endregion

        #region Macro Things

        void InlineMacro(CompiledFunction macro, StatementWithValue[] parameters, bool saveValue)
        {
            if (Constants.Keywords.Contains(macro.Identifier.Content))
            { throw new CompilerException($"The identifier \"{macro.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", macro.Identifier, macro.FilePath); }

            AddComment($"Inline {macro.ReadableID()} {{");

            int returnValueSize = 0;
            if (macro.ReturnSomething)
            { returnValueSize = GenerateInitialValue(macro.Type, "returnvalue"); }

            int paramsSize = 0;

            for (int i = 0; i < parameters.Length; i++)
            {
                StatementWithValue param = parameters[i];
                string paramName = macro.Parameters[i].Identifier.Content;
                CompiledType paramType = macro.ParameterTypes[i];

                AddComment($" Param {i}:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + paramName);

                paramsSize += paramType.SizeOnStack;
            }

            AddComment(" .:");

            macro.Identifier.AnalysedType = TokenAnalysedType.FunctionName;

            (
                CompiledParameter[] Parameters,
                KeyValuePair<string, CompiledVariable>[] Locals,
                int[] ReturnInstructions,
                string CurrentFile
            ) savedThings =
            (
                this.CompiledParameters.ToArray(),
                CompiledVariables.ToArray(),
                ReturnInstructions.ToArray(),
                CurrentFile
            );

            this.CompiledParameters.Clear();
            RemoveLocalVariables();
            ReturnInstructions.Clear();

            CompileParameters(macro.Parameters);

            CurrentFile = macro.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"macro_{macro.ReadableID()};{CurrentFile};{GeneratedCode.Count};{macro.Identifier.Position.Start.Line}");

            CanReturn = true;
            AddInstruction(Opcode.PUSH_VALUE, new DataItem(false), "RETURN_FLAG");

            OnScopeEnter(macro.Block);

            AddComment("Statements {");
            for (int i = 0; i < macro.Statements.Length; i++)
            { GenerateCodeForStatement(macro.Statements[i]); }
            AddComment("}");

            CurrentFile = null;

            CanReturn = false;

            OnScopeExit();

            AddInstruction(Opcode.POP_VALUE, "Pop RETURN_TAG");

            AddComment("Return");
            AddInstruction(Opcode.CS_POP);

            CompiledParameters.Fill(savedThings.Parameters);
            CompiledVariables.Fill(savedThings.Locals);
            ReturnInstructions.Fill(savedThings.ReturnInstructions);

            CurrentFile = savedThings.CurrentFile;

            AddComment($" Clear Params (size: {paramsSize}):");
            for (int i = 0; i < paramsSize; i++)
            { AddInstruction(Opcode.POP_VALUE); }

            if (macro.ReturnSomething && !saveValue)
            {
                AddComment($" Clear Return Value (size: {returnValueSize}):");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment($"}}");
        }

        #endregion

        void OnScopeEnter(Block block)
        {
            AddComment("Scope enter");

            CleanupStack.Push(CompileVariables(block, false));
        }

        void OnScopeExit()
        {
            AddComment("Scope exit");

            FinishJumpInstructions(ReturnInstructions);
            ReturnInstructions.Clear();

            CleanupVariables(CleanupStack.Pop());

            if (CanReturn)
            {
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, ReturnFlagOffset);
                AddInstruction(Opcode.LOGIC_NOT);
                ReturnInstructions.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            }
        }

        #region HEAP Helpers

        void BoxData(int to, int size)
        {
            AddComment($"Box: {{");
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
        void UnboxData(int from, int size)
        {
            AddComment($"Unbox: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");

                AddInstruction(Opcode.PUSH_VALUE, currentOffset + from);
                AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
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

        #endregion

        #region Addressing Helpers

        const int TagsBeforeBasePointer = 2;

        int ReturnValueOffset => -(ParametersSize() + 1 + TagsBeforeBasePointer);
        const int ReturnFlagOffset = 0;
        const int SavedBasePointerOffset = -1;
        const int SavedCodePointerOffset = -2;

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
            if (GetParameter(variable.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                return GetDataAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            {
                if (val.IsStoredInHEAP)
                { throw new InternalException($"This should never occur: trying to GetDataAddress of variable \"{val.VariableName.Content}\" wich's type is {val.Type}"); }

                addressingMode = val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE;

                return val.MemoryAddress;
            }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
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
                        if (GetParameter(_prevVar.Content, out CompiledParameter param))
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
                        if (GetParameter(_prevVar.Content, out CompiledParameter param))
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
                CompiledClass @class = prevType.Class;

                @class.AddTypeArguments(TypeArguments);
                @class.AddTypeArguments(prevType.TypeParameters);

                if (@class.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Identifier) return fieldOffset;
                    if (field.PrevStatement is Field prevField) return fieldOffset + GetFieldOffset(prevField);
                }

                @class.ClearTypeArguments();
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the <paramref name="parameter"/>'s <b>memory address on the stack</b>
        /// </summary>
        int GetDataAddress(CompiledParameter parameter) => -(ParametersSize(parameter.Index) + TagsBeforeBasePointer);
        /// <summary>
        /// Returns the <paramref name="parameter"/>'s offsetted <b>memory address on the stack</b>
        /// </summary>
        int GetDataAddress(CompiledParameter parameter, int offset) => -(ParametersSize(parameter.Index) - offset + TagsBeforeBasePointer);
        /// <summary>
        /// Returns the <paramref name="variable"/>'s <b>memory address on the stack</b>
        /// </summary>
        /// <param name="addressingMode">
        /// Can be:<br/><see cref="AddressingMode.BASEPOINTER_RELATIVE"/><br/> or <br/><see cref="AddressingMode.ABSOLUTE"/>
        /// </param>
        /// <exception cref="CompilerException"/>
        int GetBaseAddress(Identifier variable, out AddressingMode addressingMode)
        {
            if (GetParameter(variable.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                return GetDataAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            { return GetBaseAddress(val, out addressingMode); }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
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
            if (GetParameter(variable.Content, out var parameter))
            {
                if (parameter.Type.IsStruct) return false;
                if (parameter.Type.IsClass) return true;

                throw new NotImplementedException();
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            { return val.IsStoredInHEAP; }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
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

        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        CleanupItem GenerateCodeForVariable(VariableDeclaretion newVariable, bool isGlobal)
        {
            for (int i = 0; i < CompiledVariables.Count; i++)
            {
                if (CompiledVariables[i].Value.VariableName.Content == newVariable.VariableName.Content)
                {
                    Warnings.Add(new Warning($"Variable \"{CompiledVariables[i].Value.VariableName}\" already defined", CompiledVariables[i].Value.VariableName, CurrentFile));
                    return new CleanupItem(0, 0);
                }
            }

            int offset = TagCount.Last;
            if (isGlobal)
            { offset += VariablesSize + ExternalFunctionsCache.Count; }
            else
            { offset += LocalVariablesSize; }

            CompiledVariable compiledVariable = CompileVariable(newVariable, offset, isGlobal);

            CompiledVariables.Add(newVariable.VariableName.Content, compiledVariable);

            if (compiledVariable.Type.IsGeneric)
            { newVariable.Type.Identifier.AnalysedType = TokenAnalysedType.TypeParameter; }

            AddComment($"Initial value {{");

            int size = GenerateInitialValue(compiledVariable.Type, "var." + newVariable.VariableName.Content);

            AddComment("}");

            return new CleanupItem(size, 1);
        }
        CleanupItem GenerateCodeForVariable(Statement st, bool isGlobal)
        {
            if (st is VariableDeclaretion newVariable)
            {
                return GenerateCodeForVariable(newVariable, isGlobal);
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

        int VariablesSize
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    CompiledVariable variable = CompiledVariables[i].Value;
                    sum += variable.Type.SizeOnStack;
                }

                return sum;
            }
        }

        int LocalVariablesSize
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    CompiledVariable variable = CompiledVariables[i].Value;

                    if (variable.IsGlobal) continue;

                    sum += variable.Type.SizeOnStack;
                }

                return sum;
            }
        }

        void RemoveLocalVariables()
        {
            for (int i = CompiledVariables.Count - 1; i >= 0; i--)
            {
                if (CompiledVariables[i].Value.IsGlobal) continue;
                CompiledVariables.RemoveAt(i);
            }
        }

        void FinishJumpInstructions(IEnumerable<int> jumpInstructions)
            => FinishJumpInstructions(jumpInstructions, GeneratedCode.Count);
        void FinishJumpInstructions(IEnumerable<int> jumpInstructions, int jumpTo)
        {
            foreach (int jumpInstruction in jumpInstructions)
            {
                GeneratedCode[jumpInstruction].ParameterInt = jumpTo - jumpInstruction;
            }
        }

        void GenerateCodeForFunction(FunctionThingDefinition function)
        {
            if (Constants.Keywords.Contains(function.Identifier.Content))
            { throw new CompilerException($"The identifier \"{function.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.FilePath); }

            function.Identifier.AnalysedType = TokenAnalysedType.FunctionName;

            if (function is FunctionDefinition functionDefinition)
            {
                for (int i = 0; i < functionDefinition.Attributes.Length; i++)
                {
                    if (functionDefinition.Attributes[i].Identifier == "External")
                    { return; }
                }
            }

            TagCount.Push(0);

            CompiledParameters.Clear();
            RemoveLocalVariables();
            ReturnInstructions.Clear();

            CompileParameters(function.Parameters);

            CurrentFile = function.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"{function.ReadableID()};{CurrentFile};{GeneratedCode.Count};{function.Identifier.Position.Start.Line}");

            CanReturn = true;
            AddInstruction(Opcode.PUSH_VALUE, new DataItem(false), "RETURN_FLAG");
            TagCount.Last++;

            OnScopeEnter(function.Block);

            AddComment("Statements {");
            for (int i = 0; i < function.Statements.Length; i++)
            { GenerateCodeForStatement(function.Statements[i]); }
            AddComment("}");

            CurrentFile = null;

            CanReturn = false;

            OnScopeExit();

            AddInstruction(Opcode.POP_VALUE, "Pop RETURN_TAG");
            TagCount.Last--;

            AddComment("Return");
            AddInstruction(Opcode.CS_POP);
            Return();

            CompiledParameters.Clear();
            RemoveLocalVariables();
            ReturnInstructions.Clear();

            TagCount.Pop();
        }

        void GenerateCodeForTopLevelStatements(Statement[] statements)
        {
            CurrentFile = null;
            TagCount.Push(0);

            AddComment("Variables");
            CleanupStack.Push(GenerateCodeForVariable(statements, true));

            AddComment("Statements {");
            for (int i = 0; i < statements.Length; i++)
            { GenerateCodeForStatement(statements[i]); }
            AddComment("}");

            FinishJumpInstructions(ReturnInstructions);
            ReturnInstructions.Clear();

            CleanupVariables(CleanupStack.Pop());

            TagCount.Pop();
        }

        void CompileParameters(ParameterDefinition[] parameters)
        {
            int paramIndex = 0;
            int paramsSize = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                paramIndex++;
                CompiledType parameterType = new(parameters[i].Type, FindType);

                if (parameterType.IsGeneric)
                { parameters[i].Type.Identifier.AnalysedType = TokenAnalysedType.TypeParameter; }

                this.CompiledParameters.Add(new CompiledParameter(paramIndex, paramsSize, parameterType, parameters[i]));

                paramsSize += parameterType.SizeOnStack;
            }
        }

        #endregion

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

            public readonly bool GetFunctionOffset(CompiledFunction compiledFunction, out int offset)
            {
                offset = -1;
                for (int i = 0; i < Functions.Length; i++)
                {
                    if (Functions[i].IsSame(compiledFunction))
                    {
                        if (offset != -1)
                        { throw new System.Exception($"BRUH"); }
                        offset = i;
                    }
                }
                return offset != -1;
            }

            public readonly void PrintInstructions() => Result.PrintInstructions(Code);
            public static void PrintInstructions(Instruction[] code)
            {
                Console.WriteLine("\n\r === INSTRUCTIONS ===\n\r");
                int indent = 0;

                for (int i = 0; i < code.Length; i++)
                {
                    Instruction instruction = code[i];
                    if (instruction.opcode == Opcode.COMMENT)
                    {
                        if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("}"))
                        {
                            indent--;
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"{"  ".Repeat(indent)}{instruction.tag}");
                        Console.ResetColor();

                        if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("{"))
                        {
                            indent++;
                        }

                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"{"  ".Repeat(indent)} {instruction.opcode}");
                    Console.Write($" ");

                    if (instruction.Parameter.Type == RuntimeType.INT)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.Parameter.ValueInt}");
                        Console.Write($" ");
                    }
                    else if (instruction.Parameter.Type == RuntimeType.FLOAT)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.Parameter.ValueFloat}");
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
        }

        Result GenerateCode(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            Output.PrintCallback printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
        {
            base.CompiledClasses = compilerResult.Classes;
            base.CompiledStructs = compilerResult.Structs;
            this.ExternalFunctions.AddRange(compilerResult.ExternalFunctions);
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

            CompiledFunction codeEntry = GetCodeEntry();

            List<string> UsedExternalFunctions = new();

            foreach (CompiledFunction function in this.CompiledFunctions)
            {
                if (function.IsExternal)
                { UsedExternalFunctions.Add(function.ExternalFunctionName); }
            }

            foreach (CompiledOperator @operator in this.CompiledOperators)
            {
                if (@operator.IsExternal)
                { UsedExternalFunctions.Add(@operator.ExternalFunctionName); }
            }

            if (settings.ExternalFunctionsCache)
            {
                AddComment($"Create external functions cache {{");
                foreach (string function in UsedExternalFunctions)
                {
                    AddComment($"Create string \"{function}\" {{");

                    AddInstruction(Opcode.PUSH_VALUE, function.Length, $"\"{function}\".Length");
                    AddInstruction(Opcode.HEAP_ALLOC, $"(String pointer)");

                    ExternalFunctionsCache.Add(function, ExternalFunctionsCache.Count);

                    for (int i = 0; i < function.Length; i++)
                    {
                        // Prepare value
                        AddInstruction(Opcode.PUSH_VALUE, new DataItem(function[i]));

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

            int entryCallInstruction = -1;
            if (codeEntry != null)
            { entryCallInstruction = Call(-1); }

            if (ExternalFunctionsCache.Count > 0)
            {
                AddComment("Clear external functions cache {");
                for (int i = 0; i < ExternalFunctionsCache.Count; i++)
                { AddInstruction(Opcode.HEAP_DEALLOC); }
                AddComment("}");
            }

            AddInstruction(Opcode.EXIT);

            foreach (var function in this.CompiledFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                foreach (var attribute in function.Attributes)
                {
                    if (attribute.Identifier.Content != "CodeEntry") continue;
                    GeneratedCode[entryCallInstruction].ParameterInt = GeneratedCode.Count - entryCallInstruction;
                }

                AddCommentForce(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Statements.Length > 0) ? "" : " }"));
                GenerateCodeForFunction(function);
                if (function.Statements.Length > 0) AddCommentForce("}");
                CurrentContext = null;
            }

            if (codeEntry != null && GeneratedCode[entryCallInstruction].ParameterInt == -1)
            { throw new InternalException($"Failed to set code entry call instruction's parameter", CurrentFile); }

            foreach (var function in this.CompiledOperators)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                AddCommentForce(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Statements.Length > 0) ? "" : " }"));
                GenerateCodeForFunction(function);
                if (function.Statements.Length > 0) AddCommentForce("}");
                CurrentContext = null;
            }

            foreach (var function in this.CompiledGeneralFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                AddCommentForce(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Statements.Length > 0) ? "" : " }"));

                GenerateCodeForFunction(function);

                if (function.Statements.Length > 0) AddCommentForce("}");

                CurrentContext = null;
            }

            foreach (var function in this.CompilableFunctions)
            {
                CurrentContext = function.Function;
                function.Function.InstructionOffset = GeneratedCode.Count;

                foreach (var attribute in function.Function.Attributes)
                {
                    if (attribute.Identifier.Content != "CodeEntry") continue;
                    GeneratedCode[entryCallInstruction].ParameterInt = GeneratedCode.Count - entryCallInstruction;
                }

                AddTypeArguments(function.TypeArguments);

                AddCommentForce(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Statements.Length > 0) ? "" : " }"));

                GenerateCodeForFunction(function.Function);

                if (function.Function.Statements.Length > 0) AddCommentForce("}");

                CurrentContext = null;
                TypeArguments.Clear();
            }

            foreach (var function in this.CompilableOperators)
            {
                CurrentContext = function.Function;
                function.Function.InstructionOffset = GeneratedCode.Count;

                AddTypeArguments(function.TypeArguments);

                AddCommentForce(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Statements.Length > 0) ? "" : " }"));

                GenerateCodeForFunction(function.Function);

                if (function.Function.Statements.Length > 0) AddCommentForce("}");

                CurrentContext = null;
                TypeArguments.Clear();
            }

            foreach (var function in this.CompilableGeneralFunctions)
            {
                CurrentContext = function.Function;
                function.Function.InstructionOffset = GeneratedCode.Count;

                AddTypeArguments(function.TypeArguments);

                AddCommentForce(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Statements.Length > 0) ? "" : " }"));

                GenerateCodeForFunction(function.Function);

                if (function.Function.Statements.Length > 0) AddCommentForce("}");

                CurrentContext = null;
                TypeArguments.Clear();
            }

            foreach (UndefinedFunctionOffset item in UndefinedFunctionOffsets)
            {
                CompiledFunction function = item.Function;
                bool useAbsolute;

                if (item.CallStatement != null)
                { useAbsolute = false; }
                else if (item.VariableStatement != null)
                { useAbsolute = true; }
                else if (item.IndexStatement != null)
                { useAbsolute = false; }
                else
                { throw new InternalException(); }

                if (function.InstructionOffset == -1)
                { throw new InternalException($"Function {function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                int offset = useAbsolute ? function.InstructionOffset : function.InstructionOffset - item.CallInstructionIndex;
                GeneratedCode[item.CallInstructionIndex].ParameterInt = offset;
            }

            foreach (UndefinedOperatorFunctionOffset item in UndefinedOperatorFunctionOffsets)
            {
                if (item.Operator.InstructionOffset == -1)
                { throw new InternalException($"Operator {item.Operator.ReadableID()} does not have instruction offset", item.CurrentFile); }

                GeneratedCode[item.CallInstructionIndex].ParameterInt = item.Operator.InstructionOffset - item.CallInstructionIndex;
            }

            foreach (UndefinedGeneralFunctionOffset item in UndefinedGeneralFunctionOffsets)
            {
                if (item.CallStatement is ConstructorCall constructorCall)
                {
                    if (item.GeneralFunction.InstructionOffset == -1)
                    { throw new InternalException($"Constructor for type \"{constructorCall.TypeName}\" does not have instruction offset", item.CurrentFile); }

                    GeneratedCode[item.CallInstructionIndex].ParameterInt = item.GeneralFunction.InstructionOffset - item.CallInstructionIndex;
                }
                else if (item.CallStatement is KeywordCall functionCall)
                {
                    if (functionCall.Identifier.Content == "delete")
                    {
                        if (item.GeneralFunction.InstructionOffset == -1)
                        { throw new InternalException($"Constructor for \"{item.GeneralFunction.Context}\" does not have instruction offset", item.CurrentFile); }

                        GeneratedCode[item.CallInstructionIndex].ParameterInt = item.GeneralFunction.InstructionOffset - item.CallInstructionIndex;
                    }
                    else if (functionCall.Identifier.Content == "clone")
                    {
                        if (item.GeneralFunction.InstructionOffset == -1)
                        { throw new InternalException($"Cloner for \"{item.GeneralFunction.Context}\" does not have instruction offset", item.CurrentFile); }

                        GeneratedCode[item.CallInstructionIndex].ParameterInt = item.GeneralFunction.InstructionOffset - item.CallInstructionIndex;
                    }
                    else if (functionCall.Identifier.Content == "out")
                    {
                        if (item.GeneralFunction.InstructionOffset == -1)
                        { throw new InternalException($"Function {item.GeneralFunction.ReadableID()} does not have instruction offset", item.CurrentFile); }

                        GeneratedCode[item.CallInstructionIndex].ParameterInt = item.GeneralFunction.InstructionOffset - item.CallInstructionIndex;
                    }
                    else
                    { throw new NotImplementedException(); }
                }
                else
                { throw new NotImplementedException(); }
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
            Output.PrintCallback printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
        {
            CodeGenerator codeGenerator = new(settings);
            return codeGenerator.GenerateCode(
                compilerResult,
                settings,
                printCallback,
                level
                );
        }
    }
}