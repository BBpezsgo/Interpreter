using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.BBCode.Generator
{
    using Compiler;
    using Parser;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;
    using LiteralStatement = Parser.Statement.Literal;
    using ParameterCleanupItem = (int Size, bool CanDeallocate, Compiler.CompiledType Type);

    public partial class CodeGeneratorForMain : CodeGenerator
    {
        #region AddInstruction()

        void AddInstruction(Instruction instruction) => GeneratedCode.Add(instruction);
        void AddInstruction(Opcode opcode) => AddInstruction(new Instruction(opcode));
        void AddInstruction(Opcode opcode, DataItem param0) => AddInstruction(new Instruction(opcode, param0));
        void AddInstruction(Opcode opcode, int param0) => AddInstruction(new Instruction(opcode, new DataItem(param0)));

        void AddInstruction(Opcode opcode, AddressingMode addressingMode) => AddInstruction(new Instruction(opcode, addressingMode));
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, int param0) => AddInstruction(new Instruction(opcode, addressingMode, new DataItem(param0)));

        void AddComment(string comment)
        {
            if (GeneratedDebugInfo.CodeComments.TryGetValue(GeneratedCode.Count, out List<string>? comments))
            { comments.Add(comment); }
            else
            { GeneratedDebugInfo.CodeComments.Add(GeneratedCode.Count, new List<string>() { comment }); }
        }
        #endregion

        #region GenerateCodeForStatement

        void GenerateCodeForStatement(VariableDeclaration newVariable)
        {
            if (newVariable.Modifiers.Contains("const")) return;

            newVariable.VariableName.AnalyzedType = TokenAnalyzedType.VariableName;

            if (GetConstant(newVariable.VariableName.Content, out _))
            { throw new CompilerException($"Symbol name \"{newVariable.VariableName}\" conflicts with an another symbol name", newVariable.VariableName, newVariable.FilePath); }

            if (InMacro.Last)
            { throw new NotImplementedException(); }

            if (!GetVariable(newVariable.VariableName.Content, out CompiledVariable? compiledVariable))
            { throw new InternalException($"Variable \"{newVariable.VariableName.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", CurrentFile); }

            newVariable.Type.SetAnalyzedType(compiledVariable.Type);

            if (newVariable.InitialValue == null) return;

            AddComment($"New Variable \"{newVariable.VariableName.Content}\" {{");

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
                { throw new NotImplementedException(); }

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
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(true));
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

                StatementWithValue value = keywordCall.Parameters[0];
                CompiledType valueType = FindStatementType(value);

                AddInstruction(Opcode.PUSH_VALUE, valueType.Size);

                return;
            }

            if (keywordCall.FunctionName == "delete")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"delete\": required {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

                CompiledType valueType = FindStatementType(keywordCall.Parameters[0]);

                if (valueType == Type.Integer)
                {
                    GenerateCodeForStatement(keywordCall.Parameters[0], new CompiledType(Type.Integer));
                    AddInstruction(Opcode.HEAP_DEALLOC);

                    return;
                }

                if (!valueType.IsClass)
                {
                    Warnings.Add(new Warning($"The 'delete' keyword-function is only working on type class or int so I skip this shit", keywordCall.Parameters[0], CurrentFile));
                    return;
                }

                if (!GetGeneralFunction(valueType.Class, FindStatementTypes(keywordCall.Parameters), BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
                {
                    if (!GetGeneralFunctionTemplate(valueType.Class, FindStatementTypes(keywordCall.Parameters), BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> destructorTemplate))
                    {
                        GenerateCodeForStatement(keywordCall.Parameters[0], new CompiledType(Type.Integer));
                        AddInstruction(Opcode.HEAP_DEALLOC);
                        AddComment("}");

                        return;
                    }
                    destructorTemplate = AddCompilable(destructorTemplate);
                    destructor = destructorTemplate.Function;
                }

                if (!destructor.CanUse(CurrentFile))
                {
                    Errors.Add(new Error($"Destructor for type '{valueType.Class.Name.Content}' function cannot be called due to its protection level", keywordCall.Identifier, CurrentFile));
                    AddComment("}");
                    return;
                }

                AddComment(" Param0:");
                GenerateCodeForStatement(keywordCall.Parameters[0], valueType);

                AddComment(" .:");

                int jumpInstruction = Call(destructor.InstructionOffset);

                if (destructor.InstructionOffset == -1)
                { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, keywordCall, destructor, CurrentFile)); }

                AddComment(" Clear Param0:");

                AddInstruction(Opcode.POP_VALUE);

                AddComment("}");

                return;
            }

            if (keywordCall.FunctionName == "clone")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"clone\": required {1} passed {0}", keywordCall, CurrentFile); }

                CompiledType paramType = FindStatementType(keywordCall.Parameters[0]);

                if (!paramType.IsClass)
                {
                    Warnings.Add(new Warning($"The 'clone' function is only working on type class so I skip this shit", keywordCall.Parameters[0], CurrentFile));
                    return;
                }

                if (!GetGeneralFunction(paramType.Class, BuiltinFunctionNames.Cloner, out CompiledGeneralFunction? cloner))
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
                    returnValueSize = GenerateInitialValue(cloner.Type);
                }

                AddComment($" Param {0}:");
                GenerateCodeForStatement(keywordCall.Parameters[0], paramType);

                AddComment(" .:");

                int jumpInstruction = Call(cloner.InstructionOffset);

                if (cloner.InstructionOffset == -1)
                { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, keywordCall, cloner, CurrentFile)); }

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

            throw new CompilerException($"Unknown keyword \"{keywordCall.FunctionName}\"", keywordCall.Identifier, CurrentFile);
        }
        void GenerateCodeForStatement(FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "sizeof")
            {
                functionCall.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {functionCall.Parameters.Length}", functionCall, CurrentFile); }

                StatementWithValue param0 = functionCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                AddInstruction(Opcode.PUSH_VALUE, param0Type.Size);

                return;
            }

            if (GetVariable(functionCall.Identifier.Content, out CompiledVariable? compiledVariable))
            {
                functionCall.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

                if (!compiledVariable.Type.IsFunction)
                { throw new CompilerException($"Variable \"{compiledVariable.VariableName.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

                GenerateCodeForFunctionCall_Variable(functionCall, compiledVariable);
                return;
            }

            if (GetParameter(functionCall.Identifier.Content, out CompiledParameter? compiledParameter))
            {
                functionCall.Identifier.AnalyzedType = TokenAnalyzedType.ParameterName;

                if (!compiledParameter.Type.IsFunction)
                { throw new CompilerException($"Variable \"{compiledParameter.Identifier.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

                if (compiledParameter.IsRef)
                { throw new NotImplementedException(); }

                GenerateCodeForFunctionCall_Variable(functionCall, compiledParameter);
                return;
            }

            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

                string? prevFile = CurrentFile;
                IAmInContext<CompiledClass>? prevContext = CurrentContext;

                CurrentFile = macro.FilePath;
                CurrentContext = null;
                int instructionsStart = GeneratedCode.Count;

                GenerateCodeForInlinedMacro(InlineMacro(macro, functionCall.Parameters));

                GeneratedDebugInfo.FunctionInformations.Add(new FunctionInformations()
                {
                    IsValid = true,
                    IsMacro = true,
                    SourcePosition = macro.Identifier.Position,
                    Identifier = macro.Identifier.Content,
                    File = macro.FilePath,
                    ReadableIdentifier = macro.ReadableID(),
                    Instructions = (instructionsStart, GeneratedCode.Count),
                });

                CurrentContext = prevContext;
                CurrentFile = prevFile;

                return;
            }

            if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compilableFunction = AddCompilable(compilableFunction);
                compiledFunction = compilableFunction.Function;
            }

            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
            GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);
        }

        Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(FunctionCall functionCall, CompiledFunction compiledFunction)
        {
            Stack<ParameterCleanupItem> parameterCleanup = new();

            if (functionCall.PrevStatement != null)
            {
                StatementWithValue passedParameter = functionCall.PrevStatement;
                CompiledType passedParameterType = FindStatementType(passedParameter);
                AddComment(" Param prev:");
                GenerateCodeForStatement(functionCall.PrevStatement);
                parameterCleanup.Push((passedParameterType.SizeOnStack, false, passedParameterType));
            }

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
                CompiledType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

                AddComment($" Param {i}:");

                bool canDeallocate = definedParameter.Modifiers.Contains("temp");

                canDeallocate = canDeallocate && (passedParameterType.InHEAP || passedParameterType == Type.Integer);

                if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
                {
                    if (explicitDeallocate && !canDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
                }
                else
                {
                    if (explicitDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                    canDeallocate = false;
                }

                GenerateCodeForStatement(passedParameter, definedParameterType);

                parameterCleanup.Push((passedParameterType.SizeOnStack, canDeallocate, passedParameterType));
            }

            return parameterCleanup;
        }

        Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(FunctionCall functionCall, FunctionType function)
        {
            Stack<ParameterCleanupItem> parameterCleanup = new();

            if (functionCall.PrevStatement != null)
            {
                StatementWithValue passedParameter = functionCall.PrevStatement;
                CompiledType passedParameterType = FindStatementType(passedParameter);
                AddComment(" Param prev:");
                GenerateCodeForStatement(functionCall.PrevStatement);
                parameterCleanup.Push((passedParameterType.SizeOnStack, false, passedParameterType));
            }

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                CompiledType definedParameterType = function.Parameters[i];

                AddComment($" Param {i}:");

                bool canDeallocate = true; // temp type maybe?

                canDeallocate = canDeallocate && (passedParameterType.InHEAP || passedParameterType == Type.Integer);

                if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
                {
                    if (explicitDeallocate && !canDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
                }
                else
                {
                    if (explicitDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                    canDeallocate = false;
                }

                GenerateCodeForStatement(passedParameter, definedParameterType);

                parameterCleanup.Push((passedParameterType.SizeOnStack, canDeallocate, passedParameterType));
            }

            return parameterCleanup;
        }

        Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(OperatorCall functionCall, CompiledOperator compiledFunction)
        {
            Stack<ParameterCleanupItem> parameterCleanup = new();

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
                CompiledType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

                AddComment($" Param {i}:");

                bool canDeallocate = definedParameter.Modifiers.Contains("temp");

                canDeallocate = canDeallocate && (passedParameterType.InHEAP || passedParameterType == Type.Integer);

                if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
                {
                    if (explicitDeallocate && !canDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
                }
                else
                {
                    if (explicitDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                    canDeallocate = false;
                }

                GenerateCodeForStatement(passedParameter, definedParameterType);

                parameterCleanup.Push((passedParameterType.SizeOnStack, canDeallocate, passedParameterType));
            }

            return parameterCleanup;
        }

        void GenerateCodeForParameterCleanup(Stack<ParameterCleanupItem> parameterCleanup)
        {
            AddComment(" Clear Params:");
            while (parameterCleanup.Count > 0)
            {
                ParameterCleanupItem passedParameter = parameterCleanup.Pop();

                if (passedParameter.CanDeallocate && passedParameter.Size == 1)
                {
                    GenerateDeallocator(passedParameter.Type);
                    continue;
                }

                for (int i = 0; i < passedParameter.Size; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }
        }

        void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
        {
            compiledFunction.AddReference(functionCall, CurrentFile);

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

            if (compiledFunction.BuiltinFunctionName == "alloc")
            {
                GenerateCodeForStatement(functionCall.Parameters[0], new CompiledType(Type.Integer));
                AddInstruction(Opcode.HEAP_ALLOC);
                return;
            }

            AddComment($"Call {compiledFunction.ReadableID()} {{");

            Stack<ParameterCleanupItem> parameterCleanup;

            int returnValueSize = 0;
            if (compiledFunction.ReturnSomething)
            {
                AddComment($"Initial return value {{");
                returnValueSize = GenerateInitialValue(compiledFunction.Type);
                AddComment($"}}");
            }

            if (compiledFunction.IsExternal)
            {
                if (!ExternalFunctions.TryGetValue(compiledFunction.ExternalFunctionName, out ExternalFunctionBase? externalFunction))
                {
                    Errors.Add(new Error($"External function \"{compiledFunction.ExternalFunctionName}\" not found", functionCall.Identifier, CurrentFile));
                    AddComment("}");
                    return;
                }

                AddComment(" Function Name:");
                if (ExternalFunctionsCache.TryGetValue(compiledFunction.ExternalFunctionName, out int cacheAddress))
                {
                    if (compiledFunction.ExternalFunctionName.Length == 0)
                    { throw new CompilerException($"External function with length of zero", compiledFunction.GetAttribute("External"), compiledFunction.FilePath); }

                    int returnValueOffset = -2;

                    parameterCleanup = GenerateCodeForParameterPassing(functionCall, compiledFunction);
                    for (int i = 0; i < parameterCleanup.Count; i++)
                    { returnValueOffset -= parameterCleanup[i].Size; }

                    AddComment($" Function name string pointer (cache):");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.ABSOLUTE, cacheAddress);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

                    if (compiledFunction.ReturnSomething)
                    {
                        if (functionCall.SaveValue)
                        {
                            AddComment($" Store return value:");
                            for (int i = 0; i < returnValueSize; i++)
                            { AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, returnValueOffset); }
                        }
                        else
                        {
                            AddComment($" Clear return value:");
                            for (int i = 0; i < returnValueSize; i++)
                            { AddInstruction(Opcode.POP_VALUE); }
                        }
                    }

                    GenerateCodeForParameterCleanup(parameterCleanup);
                }
                else
                {
                    GenerateCodeForLiteralString(compiledFunction.ExternalFunctionName);

                    int functionNameOffset = -1;
                    int returnValueOffset = -3;

                    parameterCleanup = GenerateCodeForParameterPassing(functionCall, compiledFunction);
                    for (int i = 0; i < parameterCleanup.Count; i++)
                    {
                        functionNameOffset -= parameterCleanup[i].Size;
                        returnValueOffset -= parameterCleanup[i].Size;
                    }

                    AddComment($" Load Function Name String Pointer:");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, functionNameOffset);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

                    if (compiledFunction.ReturnSomething)
                    {
                        if (functionCall.SaveValue)
                        {
                            AddComment($" Store return value:");
                            for (int i = 0; i < returnValueSize; i++)
                            { AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, returnValueOffset); }
                        }
                        else
                        {
                            AddComment($" Clear return value:");
                            for (int i = 0; i < returnValueSize; i++)
                            { AddInstruction(Opcode.POP_VALUE); }
                        }
                    }

                    GenerateCodeForParameterCleanup(parameterCleanup);

                    AddComment(" Deallocate Function Name String:");

                    AddInstruction(Opcode.HEAP_DEALLOC);
                }

                AddComment("}");
                return;
            }

            parameterCleanup = GenerateCodeForParameterPassing(functionCall, compiledFunction);

            AddComment(" .:");

            int jumpInstruction = Call(compiledFunction.InstructionOffset);

            if (compiledFunction.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, functionCall, compiledFunction, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

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

            if (functionCall.MethodParameters.Length != functionType.Parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to function {functionType}: required {functionType.Parameters.Length} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            AddComment($"Call {functionType} {{");

            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

            Stack<ParameterCleanupItem> parameterCleanup;

            int returnValueSize = 0;
            if (functionType.ReturnSomething)
            {
                AddComment($"Initial return value {{");
                returnValueSize = GenerateInitialValue(functionType.ReturnType);
                AddComment($"}}");
            }

            parameterCleanup = GenerateCodeForParameterPassing(functionCall, functionType);

            AddComment(" .:");

            CallRuntime(compiledVariable);

            GenerateCodeForParameterCleanup(parameterCleanup);

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

            if (functionCall.MethodParameters.Length != functionType.Parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to function {functionType}: required {functionType.Parameters.Length} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            AddComment($"Call (runtime) {functionType} {{");

            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

            Stack<ParameterCleanupItem> parameterCleanup;

            int returnValueSize = 0;
            if (functionType.ReturnSomething)
            {
                AddComment($"Initial return value {{");
                returnValueSize = GenerateInitialValue(functionType.ReturnType);
                AddComment($"}}");
            }

            parameterCleanup = GenerateCodeForParameterPassing(functionCall, functionType);

            AddComment(" .:");

            CallRuntime(compiledParameter);

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (functionType.ReturnSomething && !functionCall.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }

        void GenerateCodeForStatement(AnyCall anyCall)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            {
                GenerateCodeForStatement(functionCall);
                return;
            }

            CompiledType prevType = FindStatementType(anyCall.PrevStatement);
            if (!prevType.IsFunction)
            { throw new CompilerException($"This isn't a function", anyCall.PrevStatement, CurrentFile); }

            if (TryInlineMacro(anyCall.PrevStatement, out Statement? inlined))
            {
                if (inlined is Identifier identifier)
                {
                    functionCall = new FunctionCall(null, identifier.Name, anyCall.BracketLeft, anyCall.Parameters, anyCall.BracketRight)
                    {
                        SaveValue = anyCall.SaveValue,
                        Semicolon = anyCall.Semicolon,
                    };
                    GenerateCodeForStatement(functionCall);
                    return;
                }
            }

            FunctionType functionType = prevType.Function;

            AddComment($"Call (runtime) {prevType.Function} {{");

            if (anyCall.Parameters.Length != functionType.Parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to {functionType}: required {functionType.Parameters.Length} passed {anyCall.Parameters.Length}", new Position(anyCall.Parameters), CurrentFile); }

            int returnValueSize = 0;
            if (functionType.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(functionType.ReturnType);
            }

            int paramsSize = 0;

            for (int i = 0; i < anyCall.Parameters.Length; i++)
            {
                StatementWithValue passedParameter = anyCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                CompiledType definedParameterType = functionType.Parameters[i];

                if (passedParameterType != definedParameterType)
                { throw new CompilerException($"This should be a {definedParameterType} not a {passedParameterType} (parameter {i + 1})", passedParameter, CurrentFile); }

                AddComment($" Param {i}:");
                GenerateCodeForStatement(passedParameter, definedParameterType);

                paramsSize += definedParameterType.SizeOnStack;
            }

            AddComment(" .:");

            CallRuntime(anyCall.PrevStatement);

            AddComment(" Clear Params:");
            for (int i = 0; i < paramsSize; i++)
            {
                AddInstruction(Opcode.POP_VALUE);
            }

            if (functionType.ReturnSomething && !anyCall.SaveValue)
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

            if (GetOperator(@operator, out CompiledOperator? operatorDefinition))
            {
                operatorDefinition.AddReference(@operator, CurrentFile);
                @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;

                AddComment($"Call {operatorDefinition.Identifier} {{");

                Stack<ParameterCleanupItem> parameterCleanup;

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
                    if (!ExternalFunctions.TryGetValue(operatorDefinition.ExternalFunctionName, out ExternalFunctionBase? externalFunction))
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

                    parameterCleanup = GenerateCodeForParameterPassing(@operator, operatorDefinition);
                    for (int i = 0; i < parameterCleanup.Count; i++)
                    { offset -= parameterCleanup[i].Size; }

                    AddComment($" Function name string pointer:");
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, offset);

                    AddComment(" .:");
                    AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.ParameterCount);

                    GenerateCodeForParameterCleanup(parameterCleanup);

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
                    }
                    else
                    {
                        AddInstruction(Opcode.POP_VALUE);
                    }

                    AddComment("}");
                    return;
                }

                int returnValueSize = GenerateInitialValue(operatorDefinition.Type);

                parameterCleanup = GenerateCodeForParameterPassing(@operator, operatorDefinition);

                AddComment(" .:");

                int jumpInstruction = Call(operatorDefinition.InstructionOffset);

                if (operatorDefinition.InstructionOffset == -1)
                { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, @operator, operatorDefinition, CurrentFile)); }

                GenerateCodeForParameterCleanup(parameterCleanup);

                if (!@operator.SaveValue)
                {
                    AddComment(" Clear Return Value:");
                    for (int i = 0; i < returnValueSize; i++)
                    { AddInstruction(Opcode.POP_VALUE); }
                }

                AddComment("}");
            }
            else if (LanguageConstants.Operators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (LanguageConstants.Operators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {LanguageConstants.Operators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }

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

                GenerateCodeForValueSetter(@operator.Left, @operator.Right!);
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
                case LiteralType.Integer:
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(int.Parse(literal.Value)));
                    break;
                case LiteralType.Float:
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(float.Parse(literal.Value.TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                case LiteralType.String:
                    GenerateCodeForLiteralString(literal.Value);
                    break;
                case LiteralType.Boolean:
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(bool.Parse(literal.Value) ? 1 : 0));
                    break;
                case LiteralType.Char:
                    if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", CurrentFile);
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal.Value[0]));
                    break;
                default: throw new ImpossibleException();
            }
        }

        void GenerateCodeForLiteralString(string literal)
        {
            AddComment($"Create String \"{literal}\" {{");

            AddComment("Allocate String object {");

            AddInstruction(Opcode.PUSH_VALUE, 1 + literal.Length);
            AddInstruction(Opcode.HEAP_ALLOC);

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

        void GenerateCodeForStatement(Identifier variable, CompiledType? expectedType = null)
        {
            if (GetConstant(variable.Content, out DataItem constant))
            {
                AddInstruction(Opcode.PUSH_VALUE, constant);
                return;
            }

            if (GetParameter(variable.Content, out CompiledParameter? param))
            {
                variable.Name.AnalyzedType = TokenAnalyzedType.ParameterName;
                ValueAddress address = GetBaseAddress(param);

                AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);

                if (address.IsReference)
                { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RUNTIME); }

                return;
            }

            if (GetVariable(variable.Content, out CompiledVariable? val))
            {
                variable.Name.AnalyzedType = TokenAnalyzedType.VariableName;
                StackLoad(new ValueAddress(val), val.Type.SizeOnStack);
                return;
            }

            if (GetFunction(variable.Name, expectedType, out CompiledFunction? compiledFunction))
            {
                compiledFunction.AddReference(variable, CurrentFile);
                variable.Name.AnalyzedType = TokenAnalyzedType.FunctionName;

                if (compiledFunction.InstructionOffset == -1)
                { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(GeneratedCode.Count, variable, compiledFunction, CurrentFile)); }

                AddInstruction(Opcode.PUSH_VALUE, compiledFunction.InstructionOffset);

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
                int offset = GetDataOffset(field);
                ValueAddress pointerOffset = GetBaseAddress(field);

                if (!pointerOffset.InHeap)
                { throw new CompilerException($"Field \"{field}\" is on the stack", addressGetter, CurrentFile); }

                StackLoad(pointerOffset);
                AddInstruction(Opcode.MATH_ADD, offset);
                return;
            }

            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(Pointer memoryAddressFinder)
        {
            GenerateCodeForStatement(memoryAddressFinder.PrevStatement);
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }
        void GenerateCodeForStatement(WhileLoop whileLoop)
        {
            bool conditionIsComputed = TryCompute(whileLoop.Condition, null, out DataItem computedCondition);
            if (conditionIsComputed && !computedCondition.Boolean && TrimUnreachableCode)
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
                if (!computedCondition.Boolean)
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
                CleanupItem cleanupItem = GenerateCodeForVariable(forLoop.VariableDeclaration);
                if (cleanupItem.SizeOnStack != 0)
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

            foreach (BaseBranch ifSegment in @if.Parts)
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

            foreach (int item in jumpOutInstructions)
            {
                GeneratedCode[item].ParameterInt = GeneratedCode.Count - item;
            }
        }
        void GenerateCodeForStatement(NewInstance newObject)
        {
            AddComment($"new {newObject.TypeName} {{");

            if (newObject.TypeName is not TypeInstanceSimple newObjectType)
            { throw new NotImplementedException(); }

            CompiledType instanceType = FindType(newObjectType);
            newObjectType.SetAnalyzedType(instanceType);

            if (instanceType.IsStruct)
            {
                instanceType.Struct.References?.Add(new DefinitionReference(newObjectType, CurrentFile));

                GenerateInitialValue(instanceType);
            }
            else if (instanceType.IsClass)
            {
                instanceType.Class.References?.Add(new DefinitionReference(newObjectType, CurrentFile));

                if (instanceType.Class.TemplateInfo != null)
                {
                    if (newObjectType.GenericTypes is null)
                    { throw new CompilerException($"No type arguments specified for class instance \"{instanceType}\"", newObjectType, CurrentFile); }

                    if (instanceType.Class.TemplateInfo.TypeParameters.Length != newObjectType.GenericTypes.Length)
                    { throw new CompilerException($"Wrong number of type arguments specified for class instance \"{instanceType}\": require {instanceType.Class.TemplateInfo.TypeParameters.Length} specified {newObjectType.GenericTypes.Length}", newObjectType, CurrentFile); }

                    CompiledType[] genericParameters = newObjectType.GenericTypes!.Select(v => new CompiledType(v, FindType)).ToArray();
                    instanceType.Class.AddTypeArguments(genericParameters);
                }
                else
                {
                    if (newObjectType.GenericTypes is not null)
                    { throw new CompilerException($"You should not specify type arguments for class instance \"{instanceType}\"", newObjectType, CurrentFile); }
                }

                AddInstruction(Opcode.PUSH_VALUE, instanceType.Class.Size);
                AddInstruction(Opcode.HEAP_ALLOC);

                int currentOffset = 0;
                for (int fieldIndex = 0; fieldIndex < instanceType.Class.Fields.Length; fieldIndex++)
                {
                    CompiledField field = instanceType.Class.Fields[fieldIndex];
                    AddComment($"Create Field '{field.Identifier.Content}' ({fieldIndex}) {{");

                    CompiledType? fieldType = field.Type;

                    if (fieldType.IsGeneric && !instanceType.Class.CurrentTypeArguments.TryGetValue(fieldType.Name, out fieldType))
                    { throw new CompilerException($"Type argument \"{fieldType?.Name}\" not found", field, instanceType.Class.FilePath); }

                    GenerateInitialValue2(fieldType, j =>
                    {
                        AddComment($"Save Chunk {j}:");
                        AddInstruction(Opcode.PUSH_VALUE, currentOffset);
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -3);
                        AddInstruction(Opcode.MATH_ADD);
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                        currentOffset++;
                    });
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

            CompiledType instanceType = FindType(constructorCall.TypeName);
            constructorCall.TypeName.SetAnalyzedType(instanceType);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.TypeName, CurrentFile));

            if (!GetClass(constructorCall, out CompiledClass? @class))
            { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

            if (!GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), BuiltinFunctionNames.Constructor, out CompiledGeneralFunction? constructor))
            {
                if (!GetConstructorTemplate(@class, constructorCall, out CompliableTemplate<CompiledGeneralFunction> compilableGeneralFunction))
                {
                    throw new CompilerException($"Function {constructorCall.ReadableID(FindStatementType)} not found", constructorCall.Keyword, CurrentFile);
                }
                else
                {
                    compilableGeneralFunction = AddCompilable(compilableGeneralFunction);
                    constructor = compilableGeneralFunction.Function;
                }
            }

            constructor.AddReference(constructorCall, CurrentFile);

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
                returnValueSize = GenerateInitialValue(constructor.Type);
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

                paramsSize += parameterType.SizeOnStack;
            }

            AddComment(" .:");

            int jumpInstruction = Call(constructor.InstructionOffset);

            if (constructor.InstructionOffset == -1)
            { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, constructorCall, constructor, CurrentFile)); }

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
            field.FieldName.AnalyzedType = TokenAnalyzedType.FieldName;

            CompiledType prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsEnum)
            {
                if (!prevType.Enum.GetValue(field.FieldName.Content, out DataItem enumValue))
                { throw new CompilerException($"I didn't find anything like \"{field.FieldName.Content}\" in the enum {prevType.Enum.Identifier}", field.FieldName, CurrentFile); }
                AddInstruction(Opcode.PUSH_VALUE, enumValue);
                return;
            }

            if (prevType.IsStackArray && field.FieldName == "Length")
            {
                AddInstruction(Opcode.PUSH_VALUE, prevType.StackArraySize);
                return;
            }

            if (!prevType.IsStruct && !prevType.IsClass) throw new NotImplementedException();

            CompiledType type = FindStatementType(field);

            if (!GetField(field, out CompiledField? compiledField))
            { throw new CompilerException($"Field definition \"{field.FieldName}\" not found in type \"{prevType}\"", field, CurrentFile); }

            if (CurrentContext?.Context != null)
            {
                switch (compiledField.Protection)
                {
                    case Protection.Private:
                        if (CurrentContext.Context.Name.Content != compiledField.Class!.Name.Content)
                        { throw new CompilerException($"Can not access field \"{compiledField.Identifier.Content}\" of class \"{compiledField.Class.Name}\" due to it's protection level", field, CurrentFile); }
                        break;
                    case Protection.Public:
                        break;
                    default: throw new ImpossibleException();
                }
            }

            if (IsItInHeap(field))
            {
                int offset = GetDataOffset(field);
                ValueAddress pointerOffset = GetBaseAddress(field);
                for (int i = 0; i < type.SizeOnStack; i++)
                {
                    AddComment($"{i}:");
                    HeapLoad(pointerOffset, offset + i);
                }
            }
            else
            {
                ValueAddress offset = GetDataAddress(field);
                StackLoad(offset, compiledField.Type.SizeOnStack);
            }
        }
        void GenerateCodeForStatement(IndexCall index)
        {
            CompiledType prevType = FindStatementType(index.PrevStatement);

            if (prevType.IsStackArray)
            {
                if (index.PrevStatement is not Identifier identifier)
                { throw new NotSupportedException($"Only variables/parameters supported by now", index.PrevStatement, CurrentFile); }

                if (TryCompute(index.Expression, RuntimeType.SInt32, out DataItem computedIndexData))
                {
                    if (computedIndexData.ValueSInt32 < 0 || computedIndexData.ValueSInt32 >= prevType.StackArraySize)
                    { Warnings.Add(new Warning($"Index out of range", index.Expression, CurrentFile)); }

                    if (GetParameter(identifier.Content, out CompiledParameter? param))
                    {
                        if (param.Type != prevType)
                        { throw new NotImplementedException(); }

                        ValueAddress offset = GetBaseAddress(param, (computedIndexData.ValueSInt32 * prevType.StackArrayOf.SizeOnStack));
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset.Address);

                        if (offset.IsReference)
                        { throw new NotImplementedException(); }

                        return;
                    }

                    if (GetVariable(identifier.Content, out CompiledVariable? val))
                    {
                        if (val.Type != prevType)
                        { throw new NotImplementedException(); }

                        if (val.Type.InHEAP)
                        { throw new NotImplementedException(); }

                        int offset = (computedIndexData.ValueSInt32 * prevType.StackArrayOf.SizeOnStack);
                        ValueAddress address = new(val);

                        StackLoad(address + offset, prevType.StackArrayOf.SizeOnStack);

                        return;
                    }
                }

                {
                    ValueAddress address = GetDataAddress(identifier);
                    AddInstruction(Opcode.PUSH_VALUE, address.Address);
                    if (address.BasepointerRelative)
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

            if (!GetIndexGetter(prevType, out CompiledFunction? indexer))
            {
                if (!GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index getter \"{prevType}[]\" not found", index, CurrentFile); }

                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }

            GenerateCodeForFunctionCall_Function(new FunctionCall(
                    index.PrevStatement,
                    Token.CreateAnonymous(BuiltinFunctionNames.IndexerGet),
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
                ValueAddress address = GetDataAddress(statement);

                if (address.InHeap)
                { throw new CompilerException($"This value is stored in the heap and not in the stack", statement, CurrentFile); }

                if (address.IsReference)
                {
                    AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);
                }
                else
                {
                    AddInstruction(Opcode.PUSH_VALUE, address.Address);

                    if (address.BasepointerRelative)
                    {
                        AddInstruction(Opcode.GET_BASEPOINTER);
                        AddInstruction(Opcode.MATH_ADD);
                    }
                }
                return;
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
            @as.Type.SetAnalyzedType(targetType);

            GenerateCodeForStatement(@as.PrevStatement, targetType);

            CompiledType type = FindStatementType(@as.PrevStatement, targetType);

            if (!targetType.IsFunction && type == targetType)
            {
                Hints.Add(new Hint($"Redundant type conversion", @as.Keyword, CurrentFile));
                return;
            }

            if (type.IsBuiltin && targetType.IsBuiltin)
            {
                AddInstruction(Opcode.PUSH_VALUE, new DataItem((byte)targetType.BuiltinType.Convert()));
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
                for (int i = 0; i < block.Statements.Length; i++)
                { GenerateCodeForStatement(block.Statements[i]); }
                AddComment("}");

                return;
            }

            OnScopeEnter(block);

            AddComment("Statements {");
            for (int i = 0; i < block.Statements.Length; i++)
            { GenerateCodeForStatement(block.Statements[i]); }
            AddComment("}");

            OnScopeExit();
        }

        void GenerateCodeForStatement(Statement statement, CompiledType? expectedType = null)
        {
            int startInstruction = GeneratedCode.Count;

            if (statement is LiteralList listValue)
            { GenerateCodeForStatement(listValue); }
            else if (statement is VariableDeclaration newVariable)
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
            else if (statement is AnyCall anyCall)
            { GenerateCodeForStatement(anyCall); }
            else if (statement is Block block)
            { GenerateCodeForStatement(block); }
            else
            { throw new InternalException($"Unimplemented statement {statement.GetType().Name}"); }

            GeneratedDebugInfo.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (startInstruction, GeneratedCode.Count - 1),
                SourcePosition = statement.Position,
            });
        }

        CleanupItem[] CompileVariables(Block block, bool addComments = true)
        {
            if (addComments) AddComment("Variables {");

            List<CleanupItem> result = new();

            foreach (Statement s in block.Statements)
            {
                CleanupItem item = GenerateCodeForVariable(s);
                if (item.SizeOnStack == 0) continue;

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
                    if (item.SizeOnStack != 1) throw new InternalException();
                    GenerateDeallocator(item.Type!);
                }
                else
                {
                    for (int x = 0; x < item.SizeOnStack; x++)
                    { AddInstruction(Opcode.POP_VALUE); }
                }

                CompiledVariables.RemoveAt(CompiledVariables.Count - 1);
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
            if (GetConstant(statementToSet.Content, out _))
            { throw new CompilerException($"Can not set constant value: it is readonly", statementToSet, CurrentFile); }

            if (GetParameter(statementToSet.Content, out CompiledParameter? parameter))
            {
                CompiledType valueType = FindStatementType(value, parameter.Type);

                if (parameter.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{parameter.Type.Name}\" type parameter.", value, CurrentFile); }

                GenerateCodeForStatement(value);

                if (parameter.IsRef)
                {
                    ValueAddress offset = GetBaseAddress(parameter);
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset.Address);
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RUNTIME);
                }
                else
                {
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
                }
            }
            else if (GetVariable(statementToSet.Content, out CompiledVariable? variable))
            {
                CompiledType valueType = FindStatementType(value, variable.Type);

                if (variable.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{variable.Type.Name}\" type variable.", value, CurrentFile); }

                GenerateCodeForStatement(value);
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
            }
            else
            {
                throw new CompilerException($"Variable \"{statementToSet.Content}\" not found", statementToSet, CurrentFile);
            }
        }
        void GenerateCodeForValueSetter(Field statementToSet, StatementWithValue value)
        {
            statementToSet.FieldName.AnalyzedType = TokenAnalyzedType.FieldName;

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

            if (prevType.IsClass)
            { prevType.Class.ClearTypeArguments(); }

            if (_inHeap)
            {
                int offset = GetDataOffset(statementToSet);
                ValueAddress pointerOffset = GetBaseAddress(statementToSet);
                HeapStore(pointerOffset, offset);

                return;
            }
            else
            {
                ValueAddress offset = GetDataAddress(statementToSet);
                StackStore(offset, valueType.SizeOnStack);

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

                if (TryCompute(statementToSet.Expression, RuntimeType.SInt32, out DataItem computedIndexData))
                {
                    if (computedIndexData.ValueSInt32 < 0 || computedIndexData.ValueSInt32 >= prevType.StackArraySize)
                    { Warnings.Add(new Warning($"Index out of range", statementToSet.Expression, CurrentFile)); }

                    GenerateCodeForStatement(value);

                    if (GetParameter(identifier.Content, out _))
                    { throw new NotImplementedException(); }

                    if (GetVariable(identifier.Content, out CompiledVariable? variable))
                    {
                        if (variable.Type != prevType)
                        { throw new NotImplementedException(); }

                        if (variable.Type.InHEAP)
                        { throw new NotImplementedException(); }

                        int offset = computedIndexData.ValueSInt32 * prevType.StackArrayOf.SizeOnStack;
                        StackStore(new ValueAddress(variable) + offset, prevType.StackArrayOf.Size);
                        return;
                    }
                }

                throw new NotImplementedException();
            }

            if (!prevType.IsClass)
            { throw new CompilerException($"Index setter for type \"{prevType.Name}\" not found", statementToSet, CurrentFile); }

            if (!GetIndexSetter(prevType, valueType, out CompiledFunction? indexer))
            {
                if (!GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index setter \"{prevType.Class.Name}[] = {valueType}\" not found", statementToSet, CurrentFile); }

                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }

            GenerateCodeForFunctionCall_Function(new FunctionCall(
                    statementToSet.PrevStatement,
                    Token.CreateAnonymous(BuiltinFunctionNames.IndexerSet),
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

            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }
        void GenerateCodeForValueSetter(VariableDeclaration statementToSet, StatementWithValue value)
        {
            if (GetConstant(statementToSet.VariableName.Content, out _))
            { throw new CompilerException($"Can not set constant value: it is readonly", statementToSet, statementToSet.FilePath); }

            if (value is LiteralList)
            { throw new NotImplementedException(); }

            if (!GetVariable(statementToSet.VariableName.Content, out CompiledVariable? variable))
            { throw new CompilerException($"Variable \"{statementToSet.VariableName.Content}\" not found", statementToSet.VariableName, CurrentFile); }

            CompiledType valueType = FindStatementType(value);

            AssignTypeCheck(variable.Type, valueType, value);

            if (variable.Type.IsBuiltin &&
                TryCompute(value, null, out DataItem yeah))
            {
                AddInstruction(Opcode.PUSH_VALUE, yeah);
            }
            else if (variable.Type.IsStackArray)
            {
                if (variable.Type.StackArrayOf != Type.Char)
                { throw new InternalException(); }
                if (value is not LiteralStatement literal)
                { throw new InternalException(); }
                if (literal.Type != LiteralType.String)
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

            if (variable.Type.InHEAP)
            {
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, variable.MemoryAddress);
            }
            else
            {
                int destination = variable.MemoryAddress;
                int size = variable.Type.Size;

                for (int offset = 1; offset <= size; offset++)
                { AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, destination + size - offset); }

                return;
            }
        }

        void AssignTypeCheck(CompiledType destination, CompiledType valueType, StatementWithValue value)
        {
            if (destination == valueType)
            { return; }

            if (destination.IsEnum)
            { if (CodeGenerator.SameType(destination.Enum, valueType)) return; }

            if (valueType.IsEnum)
            { if (CodeGenerator.SameType(valueType.Enum, destination)) return; }

            if (destination.IsBuiltin && destination.BuiltinType == Type.Byte &&
                TryCompute(value, null, out DataItem yeah) &&
                yeah.Type == RuntimeType.SInt32)
            { return; }

            if (value is LiteralStatement literal && literal.Type == LiteralType.String)
            {
                if (destination.IsStackArray && destination.StackArrayOf == Type.Char)
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
            AddComment($"Deallocate \"{deallocateableType}\" {{");

            if (deallocateableType == Type.Integer)
            {
                AddInstruction(Opcode.HEAP_DEALLOC);
                AddComment("}");
                return;
            }

            if (deallocateableType.IsClass)
            {
                if (!GetGeneralFunction(deallocateableType.Class, new CompiledType[] { deallocateableType }, BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
                {
                    if (!GetGeneralFunctionTemplate(deallocateableType.Class, new CompiledType[] { deallocateableType }, BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> destructorTemplate))
                    {
                        AddInstruction(Opcode.HEAP_DEALLOC);
                        AddComment("}");
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

                AddComment(" .:");

                int jumpInstruction = Call(destructor.InstructionOffset);

                if (destructor.InstructionOffset == -1)
                { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, null, destructor, CurrentFile)); }

                AddComment(" Clear Param:");

                AddInstruction(Opcode.POP_VALUE);

                AddComment("}");
                return;
            }

            AddInstruction(Opcode.HEAP_DEALLOC);
            AddComment("}");
        }

        void GenerateCodeForInlinedMacro(Statement inlinedMacro)
        {
            InMacro.Push(true);
            if (inlinedMacro is Block block)
            { GenerateCodeForStatement(block); }
            else if (inlinedMacro is KeywordCall keywordCall &&
                keywordCall.Identifier == "return" &&
                keywordCall.Parameters.Length == 1)
            { GenerateCodeForStatement(keywordCall.Parameters[0]); }
            else
            { GenerateCodeForStatement(inlinedMacro); }
            InMacro.Pop();
        }

        #endregion

        void OnScopeEnter(Block block)
        {
            CurrentScopeDebug.Push(new ScopeInformations()
            {
                Location = new SourceCodeLocation()
                {
                    Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                    SourcePosition = block.Position,
                },
                Stack = new List<StackElementInformations>(),
            });

            AddComment("Scope enter");

            CompileConstants(block.Statements);

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

            CleanupConstants();

            ScopeInformations scope = CurrentScopeDebug.Pop();
            scope.Location.Instructions.End = GeneratedCode.Count - 1;
            GeneratedDebugInfo.ScopeInformations.Add(scope);
        }

        #region GenerateCodeFor...

        CleanupItem GenerateCodeForVariable(VariableDeclaration newVariable)
        {
            if (newVariable.Modifiers.Contains("const")) return CleanupItem.Null;

            newVariable.VariableName.AnalyzedType = TokenAnalyzedType.VariableName;

            for (int i = 0; i < CompiledVariables.Count; i++)
            {
                if (CompiledVariables[i].VariableName.Content == newVariable.VariableName.Content)
                {
                    Warnings.Add(new Warning($"Variable \"{CompiledVariables[i].VariableName}\" already defined", CompiledVariables[i].VariableName, CurrentFile));
                    return CleanupItem.Null;
                }
            }

            int offset = TagCount.Last + VariablesSize;

            CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

            StackElementInformations debugInfo = new()
            {
                Kind = StackElementKind.Variable,
                Tag = compiledVariable.VariableName.Content,
                Address = offset,
                BasepointerRelative = true,
                Size = compiledVariable.Type.SizeOnStack,
            };

            if (compiledVariable.Type.InHEAP)
            { debugInfo.Type = StackElementType.HeapPointer; }
            else
            { debugInfo.Type = StackElementType.Value; }

            CurrentScopeDebug.Last.Stack.Add(debugInfo);

            CompiledVariables.Add(compiledVariable);

            newVariable.Type.SetAnalyzedType(compiledVariable.Type);

            int size;

            if (TryCompute(newVariable.InitialValue, null, out DataItem computedInitialValue))
            {
                AddComment($"Initial value {{");

                size = 1;

                AddInstruction(Opcode.PUSH_VALUE, computedInitialValue);
                compiledVariable.IsInitialized = true;

                if (size <= 0)
                { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

                AddComment("}");
            }
            else if (compiledVariable.Type.IsStackArray &&
                compiledVariable.Type.StackArrayOf == Type.Char &&
                newVariable.InitialValue is LiteralStatement literalStatement &&
                literalStatement.Type == LiteralType.String &&
                literalStatement.Value.Length == compiledVariable.Type.StackArraySize)
            {
                size = literalStatement.Value.Length;
                compiledVariable.IsInitialized = true;

                for (int i = 0; i < literalStatement.Value.Length; i++)
                {
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem(literalStatement.Value[i]));
                }
            }
            else
            {
                AddComment($"Initial value {{");

                size = GenerateInitialValue(compiledVariable.Type);

                if (size <= 0)
                { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

                AddComment("}");
            }

            if (size != compiledVariable.Type.SizeOnStack)
            { throw new InternalException($"Variable size ({compiledVariable.Type.SizeOnStack}) and initial value size ({size}) mismatch"); }

            return new CleanupItem(size, newVariable.Modifiers.Contains("temp"), compiledVariable.Type);
        }
        CleanupItem GenerateCodeForVariable(Statement st)
        {
            if (st is VariableDeclaration newVariable)
            { return GenerateCodeForVariable(newVariable); }
            return CleanupItem.Null;
        }
        CleanupItem[] GenerateCodeForVariable(Statement[] sts)
        {
            List<CleanupItem> result = new();
            for (int i = 0; i < sts.Length; i++)
            {
                CleanupItem item = GenerateCodeForVariable(sts[i]);
                if (item.SizeOnStack == 0) continue;

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
                { sum += CompiledVariables[i].Type.SizeOnStack; }
                return sum;
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
            if (LanguageConstants.Keywords.Contains(function.Identifier.Content))
            { throw new CompilerException($"The identifier \"{function.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.FilePath); }

            function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

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
            CompiledVariables.Clear();
            ReturnInstructions.Clear();

            CompileParameters(function.Parameters);

            CurrentFile = function.FilePath;

            int instructionStart = GeneratedCode.Count;

            CanReturn = true;
            AddInstruction(Opcode.PUSH_VALUE, new DataItem(false));
            TagCount.Last++;

            OnScopeEnter(function.Block ?? throw new CompilerException($"Function \"{function.ReadableID()}\" does not have a body"));

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
            {
                Address = ReturnFlagOffset,
                BasepointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = 1,
                Tag = "RETURN_FLAG",
                Type = StackElementType.Value,
            });
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
            {
                Address = SavedBasePointerOffset,
                BasepointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = 1,
                Tag = "Saved BasePointer",
                Type = StackElementType.Value,
            });
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
            {
                Address = SavedCodePointerOffset,
                BasepointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = 1,
                Tag = "Saved CodePointer",
                Type = StackElementType.Value,
            });

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                CompiledParameter p = CompiledParameters[i];

                StackElementInformations debugInfo = new()
                {
                    Address = GetBaseAddress(p).Address,
                    Kind = StackElementKind.Parameter,
                    BasepointerRelative = true,
                    Size = p.Type.SizeOnStack,
                    Tag = p.Identifier.Content,
                };
                if (p.IsRef)
                { debugInfo.Type = StackElementType.StackPointer; }
                else if (p.Type.InHEAP)
                { debugInfo.Type = StackElementType.HeapPointer; }
                else
                { debugInfo.Type = StackElementType.Value; }
                CurrentScopeDebug.Last.Stack.Add(debugInfo);
            }

            AddComment("Statements {");
            for (int i = 0; i < function.Block.Statements.Length; i++)
            { GenerateCodeForStatement(function.Block.Statements[i]); }
            AddComment("}");

            CurrentFile = null;

            CanReturn = false;

            OnScopeExit();

            AddInstruction(Opcode.POP_VALUE);
            TagCount.Last--;

            AddComment("Return");
            Return();

            GeneratedDebugInfo.FunctionInformations.Add(new FunctionInformations()
            {
                IsValid = true,
                IsMacro = false,
                SourcePosition = function.Identifier.Position,
                Identifier = function.Identifier.Content,
                File = function.FilePath,
                ReadableIdentifier = function.ReadableID(),
                Instructions = (instructionStart, GeneratedCode.Count),
            });

            CompiledParameters.Clear();
            CompiledVariables.Clear();
            ReturnInstructions.Clear();

            InMacro.Pop();
            TagCount.Pop();
        }

        void GenerateCodeForTopLevelStatements(Statement[] statements)
        {
            if (statements.Length == 0) return;

            CurrentScopeDebug.Push(new ScopeInformations()
            {
                Location = new SourceCodeLocation()
                {
                    Instructions = (GeneratedCode.Count, GeneratedCode.Count),
                    SourcePosition = new Position(statements),
                },
                Stack = new List<StackElementInformations>(),
            });

            AddComment("TopLevelStatements {");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, 0);

            CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
            {
                Address = SavedBasePointerOffset,
                BasepointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = 1,
                Tag = "Saved BasePointer",
                Type = StackElementType.Value,
            });

            CurrentFile = null;
            CurrentContext = null;
            TagCount.Push(0);
            InMacro.Push(false);
            ReturnInstructions.Push(new List<int>());

            CanReturn = true;
            AddInstruction(Opcode.PUSH_VALUE, new DataItem(false));
            CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
            {
                Address = ReturnFlagOffset,
                BasepointerRelative = true,
                Kind = StackElementKind.Internal,
                Size = 1,
                Tag = "RETURN_FLAG",
                Type = StackElementType.Value,
            });
            TagCount.Last++;

            CompileConstants(statements);

            AddComment("Variables");
            CleanupStack.Push(GenerateCodeForVariable(statements));

            AddComment("Statements {");
            for (int i = 0; i < statements.Length; i++)
            { GenerateCodeForStatement(statements[i]); }
            AddComment("}");

            FinishJumpInstructions(ReturnInstructions.Last);
            ReturnInstructions.Pop();

            CleanupVariables(CleanupStack.Pop());

            CleanupConstants();

            CanReturn = false;
            AddInstruction(Opcode.POP_VALUE);
            TagCount.Last--;

            InMacro.Pop();
            TagCount.Pop();

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RUNTIME, 0);

            AddComment("}");

            ScopeInformations scope = CurrentScopeDebug.Pop();
            scope.Location.Instructions.End = GeneratedCode.Count - 1;
            GeneratedDebugInfo.ScopeInformations.Add(scope);
        }

        void CompileParameters(ParameterDefinition[] parameters)
        {
            int paramIndex = 0;
            int paramsSize = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                paramIndex++;
                CompiledType parameterType = new(parameters[i].Type, FindType);
                parameters[i].Type.SetAnalyzedType(parameterType);

                this.CompiledParameters.Add(new CompiledParameter(paramIndex, paramsSize, parameterType, parameters[i]));

                paramsSize += parameterType.SizeOnStack;
            }
        }

        #endregion
    }
}