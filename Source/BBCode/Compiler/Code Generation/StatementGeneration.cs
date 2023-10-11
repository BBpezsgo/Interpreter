using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0051 // Remove unused private members

namespace LanguageCore.BBCode.Compiler
{
    using LanguageCore.Runtime;
    using LanguageCore.Tokenizing;
    using Parser;
    using Parser.Statement;
    using LiteralStatement = Parser.Statement.Literal;

    public partial class CodeGenerator : CodeGeneratorBase
    {
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
            newVariable.VariableName.AnalyzedType = TokenAnalysedType.VariableName;

            if (InMacro.Last)
            { throw new NotImplementedException(); }

            if (!GetVariable(newVariable.VariableName.Content, out CompiledVariable compiledVariable))
            { throw new CompilerException($"Variable \"{newVariable.VariableName.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", newVariable.VariableName, CurrentFile); }

            if (compiledVariable.Type.IsClass)
            { newVariable.Type.Identifier.AnalyzedType = TokenAnalysedType.Class; }
            else if (compiledVariable.Type.IsStruct)
            { newVariable.Type.Identifier.AnalyzedType = TokenAnalysedType.Struct; }
            else if (compiledVariable.Type.IsEnum)
            { newVariable.Type.Identifier.AnalyzedType = TokenAnalysedType.Enum; }

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
                { throw new CompilerException($"Wrong number of parameters passed to \"return\": required {0} or {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

                if (InMacro.Last)
                {
                    AddComment(" Return from macro:");

                    if (keywordCall.Parameters.Length == 1)
                    {
                        StatementWithValue returnValue = keywordCall.Parameters[0];
                        GenerateCodeForStatement(returnValue);
                    }

                    AddComment("}");
                    return;
                }

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

                AddComment(" .:");

                if (CanReturn)
                {
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(true), "RETURN_FLAG");
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, ReturnFlagOffset);
                }

                ReturnInstructions.Last.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                AddComment("}");

                return;
            }

            if (keywordCall.FunctionName == "throw")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"throw\": required {1} passed {keywordCall.Parameters}", keywordCall, CurrentFile); }

                AddComment(" Param 0:");

                StatementWithValue throwValue = keywordCall.Parameters[0];

                GenerateCodeForStatement(throwValue);
                AddInstruction(Opcode.THROW);

                return;
            }

            if (keywordCall.FunctionName == "break")
            {
                if (BreakInstructions.Count == 0)
                { throw new CompilerException($"The keyword \"break\" does not available in the current context", keywordCall.Identifier, CurrentFile); }

                if (InMacro.Last)
                { throw new NotImplementedException(); }

                BreakInstructions.Last.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                AddComment("}");

                return;
            }

            if (keywordCall.FunctionName == "sizeof")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

                StatementWithValue param0 = keywordCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                AddInstruction(Opcode.PUSH_VALUE, param0Type.SizeOnStack, $"sizeof({param0Type.Name})");

                return;
            }

            if (keywordCall.FunctionName == "delete")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"delete\": required {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

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
                { throw new CompilerException($"Wrong number of parameters passed to \"clone\": required {1} passed {0}", keywordCall, CurrentFile); }

                var paramType = FindStatementType(keywordCall.Parameters[0]);

                if (!paramType.IsClass)
                {
                    Warnings.Add(new Warning($"The 'clone' function is only working on type class so I skip this shit", keywordCall.Parameters[0], CurrentFile));
                    return;
                }

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
                { throw new CompilerException($"Wrong number of parameters passed to \"out\": required {1} passed {0}", keywordCall, CurrentFile); }

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

                        AddComment($" Function name string pointer (cache):");
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
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {functionCall.Parameters.Length}", functionCall, CurrentFile); }

                StatementWithValue param0 = functionCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                AddInstruction(Opcode.PUSH_VALUE, param0Type.SizeOnStack, $"sizeof({param0Type.Name})");

                return;
            }

            if (functionCall.FunctionName == "Alloc")
            {
                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"Alloc\": required {1} passed {functionCall.Parameters.Length}", functionCall, CurrentFile); }

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

                if (compiledParameter.IsRef)
                { throw new NotImplementedException(); }

                GenerateCodeForFunctionCall_Variable(functionCall, compiledParameter);
                return;
            }

            if (TryGetMacro(functionCall, out MacroDefinition macro))
            {
                string prevFile = CurrentFile;
                IAmInContext<CompiledClass> prevContext = CurrentContext;

                CurrentFile = macro.FilePath;
                CurrentContext = null;

                GeneratedDebugInfo.FunctionInformations.Add(GeneratedCode.Count, new FunctionInformations()
                {
                    IsValid = true,
                    IsMacro = true,
                    SourcePosition = macro.Identifier.GetPosition(),
                    Identifier = macro.Identifier.Content,
                    File = macro.FilePath,
                    ReadableIdentifier = macro.ReadableID(),
                });

                AddInstruction(Opcode.CS_PUSH, GeneratedCode.Count);

                GenerateCodeForInlinedMacro(InlineMacro(macro, functionCall.Parameters));

                AddInstruction(Opcode.CS_POP);

                CurrentContext = prevContext;
                CurrentFile = prevFile;

                return;
            }

            if (!GetFunction(functionCall, out CompiledFunction compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compilableFunction = AddCompilable(compilableFunction);
                compiledFunction = compilableFunction.Function;
            }

            GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);
        }

        void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
        {
            if (!compiledFunction.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The {compiledFunction.ReadableID()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
                return;
            }

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ReadableID()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {(compiledFunction.IsMethod ? "method" : "function")} \"{functionCall.FunctionName}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

            if (compiledFunction.IsMacro)
            { Warnings.Add(new Warning($"I can not inline macros because of lack of intelligence so I will treat this macro as a normal function.", functionCall, CurrentFile)); }

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

                    AddComment($" Function name string pointer (cache):");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress);

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

            Stack<(int Size, bool CanDeallocate, CompiledType Type)> parameterCleanup = new();

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
                CompiledType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

                AddComment($" Param {i}:");

                bool canDeallocate = passedParameterType.InHEAP && definedParameter.Modifiers.Contains("temp");

                if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
                {
                    if (explicitDeallocate && !canDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                }
                else
                { canDeallocate = false; }

                GenerateCodeForStatement(passedParameter, definedParameterType);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParameter.Identifier.Content);

                parameterCleanup.Push((passedParameterType.SizeOnStack, canDeallocate, passedParameterType));
            }

            AddComment(" .:");

            int jumpInstruction = Call(compiledFunction.InstructionOffset);

            if (compiledFunction.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedFunctionOffset(jumpInstruction, functionCall, compiledFunction, CurrentFile)); }

            AddComment(" Clear Params:");
            while (parameterCleanup.Count > 0)
            {
                var passedParameter = parameterCleanup.Pop();

                if (passedParameter.CanDeallocate && passedParameter.Size == 1)
                {
                    GenerateDeallocator(passedParameter.Type);
                    continue;
                }

                for (int i = 0; i < passedParameter.Size; i++)
                { AddInstruction(Opcode.POP_VALUE); }
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

            functionCall.Identifier.AnalyzedType = TokenAnalysedType.VariableName;

            if (functionCall.MethodParameters.Length != functionType.Parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to {functionType}: required {functionType.Parameters.Length} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

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

            functionCall.Identifier.AnalyzedType = TokenAnalysedType.VariableName;

            if (functionCall.MethodParameters.Length != functionType.Parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to {functionType}: required {functionType.Parameters.Length} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

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
            if (OptimizeCode && TryCompute(@operator, null, out DataItem predictedValue))
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
                { throw new CompilerException($"Wrong number of parameters passed to operator {operatorDefinition.ReadableID()}: required {operatorDefinition.ParameterCount} passed {@operator.ParameterCount}", @operator, CurrentFile); }

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

                    AddComment($" Function name string pointer:");
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

                Stack<(int Size, bool CanDeallocate, CompiledType Type)> parameterCleanup = new();

                for (int i = 0; i < @operator.Parameters.Length; i++)
                {
                    StatementWithValue passedParameter = @operator.Parameters[i];
                    CompiledType passedParameterType = FindStatementType(passedParameter);
                    ParameterDefinition definedParameter = operatorDefinition.Parameters[i];
                    CompiledType definedParameterType = operatorDefinition.ParameterTypes[i];

                    if (passedParameterType != definedParameterType)
                    { }

                    AddComment($" Param {i}:");

                    bool canDeallocate = passedParameterType.InHEAP && definedParameter.Modifiers.Contains("temp");

                    if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
                    {
                        if (explicitDeallocate && !canDeallocate)
                        { Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                    }
                    else
                    { canDeallocate = false; }

                    GenerateCodeForStatement(passedParameter, definedParameterType);
                    AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParameter.Identifier);

                    parameterCleanup.Push((passedParameterType.SizeOnStack, canDeallocate, passedParameterType));
                }

                AddComment(" .:");

                int jumpInstruction = Call(operatorDefinition.InstructionOffset);

                if (operatorDefinition.InstructionOffset == -1)
                { UndefinedOperatorFunctionOffsets.Add(new UndefinedOperatorFunctionOffset(jumpInstruction, @operator, operatorDefinition, CurrentFile)); }

                AddComment(" Clear Params:");
                while (parameterCleanup.Count > 0)
                {
                    var passedParameter = parameterCleanup.Pop();

                    if (passedParameter.CanDeallocate && passedParameter.Size == 1)
                    {
                        GenerateDeallocator(passedParameter.Type);
                        continue;
                    }

                    for (int i = 0; i < passedParameter.Size; i++)
                    { AddInstruction(Opcode.POP_VALUE); }
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
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {Constants.Operators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }

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
                { throw new CompilerException($"Wrong number of parameters passed to assignment operator '{@operator.Operator.Content}': required {2} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }

                GenerateCodeForValueSetter(@operator.Left, @operator.Right);
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }
        }
        void GenerateCodeForStatement(Assignment setter)
        {
            GenerateCodeForValueSetter(setter.Left, setter.Right);
        }
        void GenerateCodeForStatement(LiteralStatement literal)
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

        void GenerateCodeForLiteralString(LiteralStatement literal)
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
                int offset = GetBaseAddress(param);
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);

                if (param.IsRef)
                { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RUNTIME); }

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

                variable.Name.AnalyzedType = TokenAnalysedType.FunctionName;

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
            if (addressGetter.PrevStatement is Field field)
            {
                int offset = GetFieldOffset(field);
                int pointerOffset = GetBaseAddress(field, out AddressingMode addressingMode, out bool inHeap);

                if (!inHeap)
                { throw new CompilerException($"Field \"{field}\" is on the stack", addressGetter, CurrentFile); }

                GenerateCodeForValueGetter(pointerOffset, addressingMode);
                AddInstruction(Opcode.MATH_ADD, offset);
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
            bool conditionIsComputed = TryCompute(whileLoop.Condition, null, out DataItem computedCondition);
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

            {
                CleanupItem cleanupItem = GenerateCodeForVariable(forLoop.VariableDeclaration, CurrentContext == null);
                if (cleanupItem.Size != 0)
                { CleanupStack[^1] = new List<CleanupItem>(CleanupStack[^1]) { cleanupItem }.ToArray(); }
            }

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
                instanceType.Struct.References?.Add(new DefinitionReference(newObject.TypeName.Identifier, CurrentFile));

                GenerateInitialValue(instanceType);
            }
            else if (instanceType.IsClass)
            {
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
            { throw new CompilerException($"Wrong number of parameters passed to \"{constructorCall.TypeName}\" constructor: required {constructor.ParameterCount} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

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
            field.FieldName.AnalyzedType = TokenAnalysedType.FieldName;

            var prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsEnum)
            {
                AddInstruction(Opcode.PUSH_VALUE, prevType.Enum.Members.Get<string, CompiledEnumMember>(field.FieldName.Content).Value);
                return;
            }

            if (prevType.IsStackArray && field.FieldName == "Length")
            {
                AddInstruction(Opcode.PUSH_VALUE, prevType.StackArraySize, $"{field}");
                return;
            }

            if (!prevType.IsStruct && !prevType.IsClass) throw new NotImplementedException();

            var type = FindStatementType(field);

            if (prevType.IsClass)
            {
                if (!GetField(field, out var compiledField))
                { throw new CompilerException($"Field definition \"{field.FieldName}\" not found in class \"{prevType.Class.Name}\"", field, CurrentFile); }

                if (CurrentContext?.Context != null)
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
                int pointerOffset = GetBaseAddress(field, out AddressingMode addressingMode, out _);
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

            if (prevType.IsStackArray)
            {
                if (index.PrevStatement is not Identifier identifier)
                { throw new NotSupportedException($"Only variables/parameters supported by now", index.PrevStatement, CurrentFile); }

                if (TryCompute(index.Expression, RuntimeType.INT, out DataItem computedIndexData))
                {
                    if (computedIndexData.ValueInt < 0 || computedIndexData.ValueInt >= prevType.StackArraySize)
                    { Warnings.Add(new Warning($"Index out of range", index.Expression, CurrentFile)); }

                    if (GetParameter(identifier.Content, out CompiledParameter param))
                    {
                        if (param.Type != prevType)
                        { throw new NotImplementedException(); }

                        int offset = GetBaseAddress(param, (computedIndexData.ValueInt * prevType.StackArrayOf.SizeOnStack));
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);

                        if (param.IsRef)
                        { throw new NotImplementedException(); }

                        return;
                    }

                    if (GetVariable(identifier.Content, out CompiledVariable val))
                    {
                        if (val.Type != prevType)
                        { throw new NotImplementedException(); }

                        if (val.IsStoredInHEAP)
                        { throw new NotImplementedException(); }

                        LoadFromStack(val.MemoryAddress + (computedIndexData.ValueInt * prevType.StackArrayOf.SizeOnStack), prevType.StackArrayOf.Size, !val.IsGlobal);

                        return;
                    }
                }

                {
                    int address = GetDataAddress(identifier, out AddressingMode addressingMode);
                    AddInstruction(Opcode.PUSH_VALUE, address);
                    if (addressingMode == AddressingMode.BASEPOINTER_RELATIVE)
                    {
                        AddInstruction(Opcode.GET_BASEPOINTER);
                        AddInstruction(Opcode.MATH_ADD);
                    }

                    GenerateCodeForStatement(index.Expression);
                    AddInstruction(Opcode.MATH_ADD);

                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RUNTIME);
                    return;
                }

                throw new NotImplementedException();
            }

            if (!prevType.IsClass)
            { throw new CompilerException($"Index getter for type \"{prevType}\" not found", index, CurrentFile); }

            if (!GetIndexGetter(prevType, out CompiledFunction indexer))
            {
                if (!GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
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
        void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
        {
            StatementWithValue statement = modifiedStatement.Statement;
            Token modifier = modifiedStatement.Modifier;

            if (modifier == "ref")
            {
                if (statement is Identifier identifier)
                {
                    if (GetVariable(identifier.Content, out var variable))
                    {
                        int address = GetDataAddress(variable, out AddressingMode addressingMode);
                        AddInstruction(Opcode.PUSH_VALUE, address, $"(ref)");

                        if (addressingMode == AddressingMode.ABSOLUTE)
                        {

                        }
                        else if (addressingMode == AddressingMode.BASEPOINTER_RELATIVE)
                        {
                            AddInstruction(Opcode.GET_BASEPOINTER);
                            AddInstruction(Opcode.MATH_ADD);
                        }
                        else
                        { throw new NotImplementedException(); }

                        return;
                    }

                    if (GetParameter(identifier.Content, out var parameter))
                    {
                        int parameterOffset = GetBaseAddress(parameter);
                        if (parameter.IsRef)
                        { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, parameterOffset, $"(ref)"); }
                        else
                        { AddInstruction(Opcode.PUSH_VALUE, parameterOffset, $"(ref)"); }
                        return;
                    }

                    throw new CompilerException($"Local symbol \"{identifier.Content}\" not found");
                }
            }

            if (modifier == "temp")
            {
                GenerateCodeForStatement(statement);
                return;
            }

            throw new NotImplementedException();
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
            int startInstruction = GeneratedCode.Count;

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
            else if (statement is LiteralStatement literal)
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
            else if (statement is ModifiedStatement modifiedStatement)
            { GenerateCodeForStatement(modifiedStatement); }
            else
            { throw new InternalException($"Unimplemented statement {statement.GetType().Name}"); }

            GeneratedDebugInfo.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (startInstruction, GeneratedCode.Count - 1),
                SourcePosition = statement.GetPosition(),
            });
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

        CleanupItem[] CompileVariables(Block block, bool isGlobal, bool addComments = true)
        {
            if (addComments) AddComment("Variables {");

            List<CleanupItem> result = new();

            foreach (var s in block.Statements)
            {
                CleanupItem item = GenerateCodeForVariable(s, isGlobal);
                if (item.Size == 0) continue;

                result.Add(item);
            }

            if (addComments) AddComment("}");

            return result.ToArray();
        }

        void CleanupVariables(CleanupItem[] cleanupItems)
        {
            if (cleanupItems.Length == 0) return;
            AddComment("Clear Variables");
            for (int i = cleanupItems.Length - 1; i >= 0; i--)
            {
                CleanupItem item = cleanupItems[i];

                if (item.ShouldDeallocate)
                {
                    if (item.Size != 1) throw new InternalException();
                    GenerateDeallocator(item.Type);
                }
                else
                {
                    for (int x = 0; x < item.Size; x++)
                    { AddInstruction(Opcode.POP_VALUE); }
                }

                CompiledVariables.Remove(CompiledVariables[^1].Key);
            }
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
            if (GetParameter(statementToSet.Content, out CompiledParameter parameter))
            {
                CompiledType valueType = FindStatementType(value, parameter.Type);

                if (parameter.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{parameter.Type.Name}\" type parameter.", value, CurrentFile); }

                GenerateCodeForStatement(value);

                if (parameter.IsRef)
                {
                    int offset = GetBaseAddress(parameter);
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RUNTIME, parameter.RealIndex);
                }
                else
                {
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
                }
            }
            else if (GetVariable(statementToSet.Content, out CompiledVariable variable))
            {
                CompiledType valueType = FindStatementType(value, variable.Type);

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
            statementToSet.FieldName.AnalyzedType = TokenAnalysedType.FieldName;

            CompiledType valueType = FindStatementType(value);

            CompiledType prevType = FindStatementType(statementToSet.PrevStatement);

            if (!prevType.IsStruct && !prevType.IsClass)
            { throw new NotImplementedException(); }

            CompiledType type = FindStatementType(statementToSet);

            if (type != valueType)
            { throw new CompilerException($"Can not set a \"{valueType}\" type value to the \"{type}\" type field.", value, CurrentFile); }

            if (prevType.IsClass)
            { prevType.Class.AddTypeArguments(prevType.TypeParameters); }

            GenerateCodeForStatement(value);

            bool _inHeap = IsItInHeap(statementToSet);

            if (_inHeap)
            {
                int offset = GetFieldOffset(statementToSet);
                int pointerOffset = GetBaseAddress(statementToSet, out AddressingMode addressingMode, out _);
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

            if (prevType.IsStackArray)
            {
                if (statementToSet.PrevStatement is not Identifier identifier)
                { throw new NotSupportedException($"Only variables/parameters supported by now", statementToSet.PrevStatement, CurrentFile); }

                if (TryCompute(statementToSet.Expression, RuntimeType.INT, out DataItem computedIndexData))
                {
                    if (computedIndexData.ValueInt < 0 || computedIndexData.ValueInt >= prevType.StackArraySize)
                    { Warnings.Add(new Warning($"Index out of range", statementToSet.Expression, CurrentFile)); }

                    GenerateCodeForStatement(value);

                    if (GetParameter(identifier.Content, out CompiledParameter param))
                    { throw new NotImplementedException(); }

                    if (GetVariable(identifier.Content, out CompiledVariable variable))
                    {
                        if (variable.Type != prevType)
                        { throw new NotImplementedException(); }

                        if (variable.IsStoredInHEAP)
                        { throw new NotImplementedException(); }

                        StoreToStack(variable.MemoryAddress + (computedIndexData.ValueInt * prevType.StackArrayOf.SizeOnStack), prevType.StackArrayOf.Size, !variable.IsGlobal);
                        return;
                    }
                }

                throw new NotImplementedException();
            }

            if (!prevType.IsClass)
            { throw new CompilerException($"Index setter for type \"{prevType.Name}\" not found", statementToSet, CurrentFile); }

            if (!GetIndexSetter(prevType, valueType, out CompiledFunction indexer))
            {
                if (!GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
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

            if (variable.IsInitialized)
            { return; }

            CompiledType valueType = FindStatementType(value);

            AssignTypeCheck(variable.Type, valueType, value);

            if (variable.Type.IsBuiltin &&
                TryCompute(value, null, out DataItem yeah))
            {
                AddInstruction(Opcode.PUSH_VALUE, yeah);
            }
            else if (variable.Type.IsStackArray)
            {
                if (variable.Type.StackArrayOf != Type.CHAR)
                { throw new InternalException(); }
                if (value is not LiteralStatement literal)
                { throw new InternalException(); }
                if (literal.Type != LiteralType.STRING)
                { throw new InternalException(); }
                if (literal.Value.Length != variable.Type.StackArraySize)
                { throw new InternalException(); }

                for (int i = 0; i < literal.Value.Length; i++)
                {
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal.Value[i]));
                }
            }
            else
            {
                GenerateCodeForStatement(value);
            }

            variable.IsInitialized = true;

            if (variable.IsStoredInHEAP)
            {
                AddInstruction(Opcode.STORE_VALUE, variable.AddressingMode(), variable.MemoryAddress);
            }
            else
            {
                int destination = variable.MemoryAddress;
                int size = variable.Type.Size;
                AddressingMode addressingMode = variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE;

                for (int offset = 1; offset <= size; offset++)
                { AddInstruction(Opcode.STORE_VALUE, addressingMode, destination + size - offset); }

                return;
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

            if (destination.IsBuiltin && destination.BuiltinType == Type.BYTE &&
                TryCompute(value, null, out DataItem yeah) &&
                yeah.Type == RuntimeType.INT)
            { return; }

            if (value is LiteralStatement literal && literal.Type == LiteralType.STRING)
            {
                if (destination.IsStackArray && destination.StackArrayOf == Type.CHAR)
                {
                    string literalValue = literal.Value;
                    if (literalValue.Length != destination.StackArraySize)
                    { throw new CompilerException($"Can not set \"{literalValue}\" (size of {literalValue.Length}) value to stack array {destination} (size of {destination.StackArraySize}) variable.", value, CurrentFile); }
                    return;
                }
            }

            throw new CompilerException($"Can not set a {valueType} type value to the {destination} type variable.", value, CurrentFile);
        }

        void GenerateDeallocator(CompiledType deallocateableType)
        {
            if (deallocateableType == Type.INT)
            {
                AddInstruction(Opcode.HEAP_DEALLOC);
                return;
            }

            if (deallocateableType.IsClass)
            {
                if (!GetGeneralFunction(deallocateableType.Class, new CompiledType[] { deallocateableType }, FunctionNames.Destructor, out var destructor))
                {
                    if (!GetGeneralFunctionTemplate(deallocateableType.Class, new CompiledType[] { deallocateableType }, FunctionNames.Destructor, out var destructorTemplate))
                    {
                        AddInstruction(Opcode.HEAP_DEALLOC);
                        return;
                    }
                    destructorTemplate = AddCompilable(destructorTemplate);
                    destructor = destructorTemplate.Function;
                }

                if (!destructor.CanUse(CurrentFile))
                {
                    Errors.Add(new Error($"Destructor for type '{deallocateableType.Class.Name.Content}' function cannot be called due to its protection level", null, CurrentFile));
                    AddComment("}");
                    return;
                }

                AddComment(" Param0 (should be already be there):");
                AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");

                AddComment(" .:");

                int jumpInstruction = Call(destructor.InstructionOffset);

                if (destructor.InstructionOffset == -1)
                { UndefinedGeneralFunctionOffsets.Add(new UndefinedGeneralFunctionOffset(jumpInstruction, null, destructor, CurrentFile)); }

                AddComment(" Clear Param:");

                AddInstruction(Opcode.POP_VALUE);

                AddComment("}");

                return;
            }

            AddInstruction(Opcode.HEAP_DEALLOC);
        }

        void GenerateCodeForInlinedMacro(Statement inlinedMacro)
        {
            InMacro.Push(true);
            if (inlinedMacro is Block block)
            { GenerateCodeForStatement(block); }
            else
            { GenerateCodeForStatement(inlinedMacro); }
            InMacro.Pop();
        }

        #endregion

        void OnScopeEnter(Block block)
        {
            AddComment("Scope enter");

            CleanupStack.Push(CompileVariables(block, CurrentContext == null));
            ReturnInstructions.Push(new List<int>());
        }

        void OnScopeExit()
        {
            AddComment("Scope exit");

            FinishJumpInstructions(ReturnInstructions.Last);
            ReturnInstructions.Pop();

            CleanupVariables(CleanupStack.Pop());

            if (CanReturn)
            {
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, ReturnFlagOffset);
                AddInstruction(Opcode.LOGIC_NOT);
                ReturnInstructions.Last.Add(GeneratedCode.Count);
                AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            }
        }

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
                    return CleanupItem.Null;
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
            { newVariable.Type.Identifier.AnalyzedType = TokenAnalysedType.TypeParameter; }

            int size;

            if (TryCompute(newVariable.InitialValue, null, out DataItem computedInitialValue))
            {
                AddComment($"Initial value {{");

                size = 1;

                AddInstruction(Opcode.PUSH_VALUE, computedInitialValue, "var." + newVariable.VariableName.Content);
                compiledVariable.IsInitialized = true;

                if (size <= 0)
                { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

                AddComment("}");
            }
            else if (compiledVariable.Type.IsStackArray &&
                compiledVariable.Type.StackArrayOf == Type.CHAR &&
                newVariable.InitialValue is LiteralStatement literalStatement &&
                literalStatement.Type == LiteralType.STRING &&
                literalStatement.Value.Length == compiledVariable.Type.StackArraySize)
            {
                size = literalStatement.Value.Length;
                compiledVariable.IsInitialized = true;

                for (int i = 0; i < literalStatement.Value.Length; i++)
                {
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(literalStatement.Value[i]), $"var.{newVariable.VariableName.Content}[{i}]");
                }
            }
            else
            {
                AddComment($"Initial value {{");

                size = GenerateInitialValue(compiledVariable.Type, $"var.{newVariable.VariableName.Content}");

                if (size <= 0)
                { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

                AddComment("}");
            }

            if (size != compiledVariable.Type.SizeOnStack)
            { throw new InternalException($"Variable size ({compiledVariable.Type.SizeOnStack}) and initial value size ({size}) mismatch"); }

            return new CleanupItem(size, newVariable.Modifiers.Contains("temp"), compiledVariable.Type);
        }
        CleanupItem GenerateCodeForVariable(Statement st, bool isGlobal)
        {
            if (st is VariableDeclaretion newVariable)
            {
                return GenerateCodeForVariable(newVariable, isGlobal);
            }
            return CleanupItem.Null;
        }
        CleanupItem[] GenerateCodeForVariable(Statement[] sts, bool isGlobal)
        {
            List<CleanupItem> result = new();
            for (int i = 0; i < sts.Length; i++)
            {
                CleanupItem item = GenerateCodeForVariable(sts[i], isGlobal);
                if (item.Size == 0) continue;

                result.Add(item);
            }
            return result.ToArray();
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
        int LocalVariablesSizeAfter(CompiledVariable variable)
        {
            int sum = 0;

            for (int i = IndexOfVariable(variable) + 1; i < CompiledVariables.Count; i++)
            {
                CompiledVariable _variable = CompiledVariables[i].Value;

                if (_variable.IsGlobal) continue;

                sum += _variable.Type.SizeOnStack;
            }

            return sum;
        }
        int IndexOfVariable(CompiledVariable variable)
        {
            for (int i = 0; i < CompiledVariables.Count; i++)
            {
                CompiledVariable _variable = CompiledVariables[i].Value;

                if (_variable.VariableName.Content == variable.VariableName.Content)
                { return i; }
            }
            return -1;
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

            function.Identifier.AnalyzedType = TokenAnalysedType.FunctionName;

            if (function is FunctionDefinition functionDefinition)
            {
                for (int i = 0; i < functionDefinition.Attributes.Length; i++)
                {
                    if (functionDefinition.Attributes[i].Identifier == "External")
                    { return; }
                }
            }

            TagCount.Push(0);
            InMacro.Push(false);

            CompiledParameters.Clear();
            RemoveLocalVariables();
            ReturnInstructions.Clear();

            CompileParameters(function.Parameters);

            CurrentFile = function.FilePath;

            GeneratedDebugInfo.FunctionInformations.Add(GeneratedCode.Count, new FunctionInformations()
            {
                IsValid = true,
                IsMacro = false,
                SourcePosition = function.Identifier.GetPosition(),
                Identifier = function.Identifier.Content,
                File = function.FilePath,
                ReadableIdentifier = function.ReadableID(),
            });

            AddInstruction(Opcode.CS_PUSH, GeneratedCode.Count);

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

            InMacro.Pop();
            TagCount.Pop();
        }

        void GenerateCodeForTopLevelStatements(Statement[] statements)
        {
            AddComment("TopLevelStatements {");

            CurrentFile = null;
            CurrentContext = null;
            TagCount.Push(0);
            InMacro.Push(false);
            ReturnInstructions.Push(new List<int>());

            AddComment("Variables");
            CleanupStack.Push(GenerateCodeForVariable(statements, true));

            AddComment("Statements {");
            for (int i = 0; i < statements.Length; i++)
            { GenerateCodeForStatement(statements[i]); }
            AddComment("}");

            FinishJumpInstructions(ReturnInstructions.Last);
            ReturnInstructions.Pop();

            CleanupVariables(CleanupStack.Pop());

            InMacro.Pop();
            TagCount.Pop();

            AddComment("}");
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
                { parameters[i].Type.Identifier.AnalyzedType = TokenAnalysedType.TypeParameter; }

                this.CompiledParameters.Add(new CompiledParameter(paramIndex, paramsSize, parameterType, parameters[i]));

                paramsSize += parameterType.SizeOnStack;
            }
        }

        #endregion
    }
}