namespace LanguageCore.BBCode.Generator;

using Compiler;
using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;
using LiteralStatement = Parser.Statement.Literal;
using ParameterCleanupItem = (int Size, bool CanDeallocate, Compiler.GeneralType Type);

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
                GeneralType returnValueType = FindStatementType(returnValue);

                GenerateCodeForStatement(returnValue);

                if (InFunction || InMacro.Last)
                {
                    int offset = ReturnValueOffset;
                    for (int i = 0; i < returnValueType.Size; i++)
                    { AddInstruction(Opcode.STORE_VALUE, AddressingMode.BasePointerRelative, offset - i); }
                }
                else
                {
                    if (returnValueType is not BuiltinType)
                    { throw new CompilerException($"Exit code must be a built-in type (not {returnValueType})", returnValue, CurrentFile); }

                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.Absolute, 0);
                }
            }

            AddComment(" .:");

            if (CanReturn)
            {
                AddInstruction(Opcode.PUSH_VALUE, new DataItem(true));
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BasePointerRelative, ReturnFlagOffset);
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
            GeneralType valueType = FindStatementType(value);

            OnGotStatementType(keywordCall, new BuiltinType(BasicType.Integer));
            keywordCall.PredictedValue = valueType.Size;

            AddInstruction(Opcode.PUSH_VALUE, valueType.Size);

            return;
        }

        if (keywordCall.FunctionName == "delete")
        {
            if (keywordCall.Parameters.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"delete\": required {1} passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

            GeneralType deletableType = FindStatementType(keywordCall.Parameters[0]);

            if (deletableType is not PointerType)
            {
                AnalysisCollection?.Warnings.Add(new Warning($"The \"delete\" keyword-function is only working on pointers or pointer so I skip this", keywordCall.Parameters[0], CurrentFile));
                return;
            }

            if (!GetGeneralFunction(deletableType, FindStatementTypes(keywordCall.Parameters).ToArray(), BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
            {
                if (!GetGeneralFunctionTemplate(deletableType, FindStatementTypes(keywordCall.Parameters).ToArray(), BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> destructorTemplate))
                {
                    GenerateCodeForStatement(keywordCall.Parameters[0], new BuiltinType(BasicType.Integer));
                    AddInstruction(Opcode.HEAP_FREE);
                    AddComment("}");

                    AnalysisCollection?.Warnings.Add(new Warning($"Destructor for type \"{deletableType}\" not found", keywordCall.Identifier, CurrentFile));

                    return;
                }
                destructorTemplate = AddCompilable(destructorTemplate);
                destructor = destructorTemplate.Function;
            }

            if (!destructor.CanUse(CurrentFile))
            {
                AnalysisCollection?.Errors.Add(new Error($"Destructor for type \"{deletableType}\" function cannot be called due to its protection level", keywordCall.Identifier, CurrentFile));
                AddComment("}");
                return;
            }

            AddComment(" Param0:");
            GenerateCodeForStatement(keywordCall.Parameters[0], deletableType);

            AddComment(" .:");

            int jumpInstruction = Call(destructor.InstructionOffset);

            if (destructor.InstructionOffset == -1)
            { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, false, keywordCall, destructor, CurrentFile)); }

            AddComment(" Clear Param0:");

            AddInstruction(Opcode.HEAP_FREE);

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
            GeneralType param0Type = FindStatementType(param0);

            OnGotStatementType(functionCall, new BuiltinType(BasicType.Integer));
            functionCall.PredictedValue = param0Type.Size;

            AddInstruction(Opcode.PUSH_VALUE, param0Type.Size);

            return;
        }

        if (GetVariable(functionCall.Identifier.Content, out CompiledVariable? compiledVariable))
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;
            functionCall.Reference = compiledVariable;

            if (compiledVariable.Type is not FunctionType)
            { throw new CompilerException($"Variable \"{compiledVariable.VariableName.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

            GenerateCodeForFunctionCall_Variable(functionCall, compiledVariable);
            return;
        }

        if (GetParameter(functionCall.Identifier.Content, out CompiledParameter? compiledParameter))
        {
            if (functionCall.Identifier.Content != "this")
            { functionCall.Identifier.AnalyzedType = TokenAnalyzedType.ParameterName; }
            functionCall.Reference = compiledParameter;

            if (compiledParameter.Type is not FunctionType)
            { throw new CompilerException($"Variable \"{compiledParameter.Identifier.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

            if (compiledParameter.IsRef)
            { throw new NotImplementedException(); }

            GenerateCodeForFunctionCall_Variable(functionCall, compiledParameter);
            return;
        }

        if (TryGetMacro(functionCall, out MacroDefinition? macro))
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
            functionCall.Reference = macro;

            Uri? prevFile = CurrentFile;
            ISameCheck? prevContext = CurrentContext;

            CurrentFile = macro.FilePath;
            CurrentContext = null;
            int instructionsStart = GeneratedCode.Count;

            if (!InlineMacro(macro, out Statement? inlinedMacro, functionCall.Parameters))
            { throw new CompilerException($"Failed to inline the macro", functionCall, CurrentFile); }

            GenerateCodeForInlinedMacro(inlinedMacro);

            GeneratedDebugInfo.FunctionInformations.Add(new FunctionInformations()
            {
                IsValid = true,
                IsMacro = true,
                SourcePosition = macro.Identifier.Position,
                Identifier = macro.Identifier.Content,
                File = macro.FilePath,
                ReadableIdentifier = macro.ToReadable(),
                Instructions = (instructionsStart, GeneratedCode.Count),
            });

            CurrentContext = prevContext;
            CurrentFile = prevFile;

            return;
        }

        if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
        {
            if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            { throw new CompilerException($"Function {functionCall.ToReadable(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

            compilableFunction = AddCompilable(compilableFunction);
            compiledFunction = compilableFunction.Function;
        }

        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
        functionCall.Reference = compiledFunction;

        if (TryEvaluate(compiledFunction, functionCall.MethodParameters, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            functionCall.PredictedValue = returnValue.Value;
            AddInstruction(Opcode.PUSH_VALUE, returnValue.Value);
            return;
        }

        GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        if (functionCall.PrevStatement != null)
        {
            StatementWithValue passedParameter = functionCall.PrevStatement;
            GeneralType passedParameterType = FindStatementType(passedParameter);
            AddComment(" Param prev:");
            GenerateCodeForStatement(functionCall.PrevStatement);
            parameterCleanup.Push((passedParameterType.Size, false, passedParameterType));
        }

        for (int i = 0; i < functionCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Parameters[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
            GeneralType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

            AddComment($" Param {i}:");

            bool canDeallocate = definedParameter.Modifiers.Contains("temp");

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(passedParameter, definedParameterType);

            parameterCleanup.Push((passedParameterType.Size, canDeallocate, passedParameterType));
        }

        return parameterCleanup;
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(FunctionCall functionCall, FunctionType function)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        if (functionCall.PrevStatement != null)
        {
            StatementWithValue passedParameter = functionCall.PrevStatement;
            GeneralType passedParameterType = FindStatementType(passedParameter);
            AddComment(" Param prev:");
            GenerateCodeForStatement(functionCall.PrevStatement);
            parameterCleanup.Push((passedParameterType.Size, false, passedParameterType));
        }

        for (int i = 0; i < functionCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Parameters[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            GeneralType definedParameterType = function.Parameters[i];

            AddComment($" Param {i}:");

            bool canDeallocate = true; // temp type maybe?

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(passedParameter, definedParameterType);

            parameterCleanup.Push((passedParameterType.Size, canDeallocate, passedParameterType));
        }

        return parameterCleanup;
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(OperatorCall functionCall, CompiledOperator compiledFunction)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        for (int i = 0; i < functionCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Parameters[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
            GeneralType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

            AddComment($" Param {i}:");

            bool canDeallocate = definedParameter.Modifiers.Contains("temp");

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(passedParameter, definedParameterType);

            parameterCleanup.Push((passedParameterType.Size, canDeallocate, passedParameterType));
        }

        return parameterCleanup;
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(ConstructorCall constructorCall, CompiledConstructor constructor)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        for (int i = 0; i < constructorCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = constructorCall.Parameters[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            ParameterDefinition definedParameter = constructor.Parameters[constructor.IsMethod ? (i + 1) : i];
            GeneralType definedParameterType = constructor.ParameterTypes[constructor.IsMethod ? (i + 1) : i];

            AddComment($" Param {i}:");

            bool canDeallocate = definedParameter.Modifiers.Contains("temp");

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(passedParameter, definedParameterType);

            parameterCleanup.Push((passedParameterType.Size, canDeallocate, passedParameterType));
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
        compiledFunction.References.Add((functionCall, CurrentFile, CurrentContext));
        OnGotStatementType(functionCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new Error($"The {compiledFunction.ToReadable()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
            return;
        }

        if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
        { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ToReadable()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

        if (functionCall.IsMethodCall != compiledFunction.IsMethod)
        { throw new CompilerException($"You called the {(compiledFunction.IsMethod ? "method" : "function")} \"{functionCall.FunctionName}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

        if (compiledFunction.IsMacro)
        { AnalysisCollection?.Warnings.Add(new Warning($"I can not inline macros because of lack of intelligence so I will treat this macro as a normal function.", functionCall, CurrentFile)); }

        if (compiledFunction.BuiltinFunctionName == "alloc")
        {
            GenerateCodeForStatement(functionCall.Parameters[0], new BuiltinType(BasicType.Integer));
            AddInstruction(Opcode.HEAP_ALLOC);
            return;
        }

        if (compiledFunction.BuiltinFunctionName == "free")
        {
            GenerateCodeForStatement(functionCall.Parameters[0], new BuiltinType(BasicType.Integer));
            AddInstruction(Opcode.HEAP_FREE);
            return;
        }

        AddComment($"Call {compiledFunction.ToReadable()} {{");

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
                AnalysisCollection?.Errors.Add(new Error($"External function \"{compiledFunction.ExternalFunctionName}\" not found", functionCall.Identifier, CurrentFile));
                AddComment("}");
                return;
            }

            AddComment(" Function Name:");
            if (ExternalFunctionsCache.TryGetValue(compiledFunction.ExternalFunctionName, out int cacheAddress))
            {
                if (compiledFunction.ExternalFunctionName.Length == 0)
                { throw new CompilerException($"External function with length of zero", compiledFunction.Attributes.Get("External"), compiledFunction.FilePath); }

                int returnValueOffset = -2;

                parameterCleanup = GenerateCodeForParameterPassing(functionCall, compiledFunction);
                for (int i = 0; i < parameterCleanup.Count; i++)
                { returnValueOffset -= parameterCleanup[i].Size; }

                AddComment($" Function name string pointer (cache):");
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.Absolute, cacheAddress);

                AddComment(" .:");
                AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.Parameters.Length);

                if (compiledFunction.ReturnSomething)
                {
                    if (functionCall.SaveValue)
                    {
                        AddComment($" Store return value:");
                        for (int i = 0; i < returnValueSize; i++)
                        { AddInstruction(Opcode.STORE_VALUE, AddressingMode.StackRelative, returnValueOffset); }
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
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, functionNameOffset);

                AddComment(" .:");
                AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.Parameters.Length);

                if (compiledFunction.ReturnSomething)
                {
                    if (functionCall.SaveValue)
                    {
                        AddComment($" Store return value:");
                        for (int i = 0; i < returnValueSize; i++)
                        { AddInstruction(Opcode.STORE_VALUE, AddressingMode.StackRelative, returnValueOffset); }
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

                AddInstruction(Opcode.HEAP_FREE);
            }

            AddComment("}");
            return;
        }

        parameterCleanup = GenerateCodeForParameterPassing(functionCall, compiledFunction);

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset);

        if (compiledFunction.InstructionOffset == -1)
        { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(jumpInstruction, false, functionCall, compiledFunction, CurrentFile)); }

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
        FunctionType functionType = (compiledVariable.Type as FunctionType)!;
        OnGotStatementType(functionCall, (compiledVariable.Type as FunctionType)!.ReturnType);

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
        FunctionType functionType = (compiledParameter.Type as FunctionType)!;
        OnGotStatementType(functionCall, (compiledParameter.Type as FunctionType)!.ReturnType);

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

            anyCall.PredictedValue = functionCall.PredictedValue;

            if (functionCall.CompiledType is not null)
            { OnGotStatementType(anyCall, functionCall.CompiledType); }

            if (anyCall.PrevStatement is IReferenceableTo _ref1)
            { _ref1.Reference = functionCall.Reference; }
            else
            { anyCall.Reference = functionCall.Reference; }
            return;
        }

        GeneralType prevType = FindStatementType(anyCall.PrevStatement);
        if (prevType is not FunctionType functionType)
        { throw new CompilerException($"This isn't a function", anyCall.PrevStatement, CurrentFile); }

        if (TryInlineMacro(anyCall.PrevStatement, out Statement? inlined))
        {
            if (inlined is Identifier identifier)
            {
                functionCall = new FunctionCall(null, identifier.Token, anyCall.BracketLeft, anyCall.Parameters, anyCall.BracketRight)
                {
                    SaveValue = anyCall.SaveValue,
                    Semicolon = anyCall.Semicolon,
                };
                GenerateCodeForStatement(functionCall);
                return;
            }
        }

        AddComment($"Call (runtime) {prevType} {{");

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
            GeneralType passedParameterType = FindStatementType(passedParameter);
            GeneralType definedParameterType = functionType.Parameters[i];

            if (passedParameterType != definedParameterType)
            { throw new CompilerException($"This should be a {definedParameterType} not a {passedParameterType} (parameter {i + 1})", passedParameter, CurrentFile); }

            AddComment($" Param {i}:");
            GenerateCodeForStatement(passedParameter, definedParameterType);

            paramsSize += definedParameterType.Size;
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
        if (Settings.OptimizeCode && TryCompute(@operator, out DataItem predictedValue))
        {
            OnGotStatementType(@operator, new BuiltinType(predictedValue.Type));
            @operator.PredictedValue = predictedValue;

            AddInstruction(Opcode.PUSH_VALUE, predictedValue);
            return;
        }

        if (GetOperator(@operator, out CompiledOperator? operatorDefinition))
        {
            operatorDefinition.References.Add((@operator, CurrentFile, CurrentContext));
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            AddComment($"Call {operatorDefinition.Identifier} {{");

            Stack<ParameterCleanupItem> parameterCleanup;

            if (!operatorDefinition.CanUse(CurrentFile))
            {
                AnalysisCollection?.Errors.Add(new Error($"The {operatorDefinition.ToReadable()} operator cannot be called due to its protection level", @operator.Operator, CurrentFile));
                AddComment("}");
                return;
            }

            if (@operator.ParameterCount != operatorDefinition.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator {operatorDefinition.ToReadable()}: required {operatorDefinition.ParameterCount} passed {@operator.ParameterCount}", @operator, CurrentFile); }

            if (operatorDefinition.IsExternal)
            {
                if (!ExternalFunctions.TryGetValue(operatorDefinition.ExternalFunctionName, out ExternalFunctionBase? externalFunction))
                {
                    AnalysisCollection?.Errors.Add(new Error($"External function \"{operatorDefinition.ExternalFunctionName}\" not found", @operator.Operator, CurrentFile));
                    AddComment("}");
                    return;
                }

                AddComment(" Function Name:");
                if (ExternalFunctionsCache.TryGetValue(operatorDefinition.ExternalFunctionName, out int cacheAddress))
                { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.Absolute, cacheAddress); }
                else
                { GenerateCodeForLiteralString(operatorDefinition.ExternalFunctionName); }

                int offset = -1;

                parameterCleanup = GenerateCodeForParameterPassing(@operator, operatorDefinition);
                for (int i = 0; i < parameterCleanup.Count; i++)
                { offset -= parameterCleanup[i].Size; }

                AddComment($" Function name string pointer:");
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, offset);

                AddComment(" .:");
                AddInstruction(Opcode.CALL_EXTERNAL, externalFunction.Parameters.Length);

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

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, thereIsReturnValue ? -2 : -1);
                AddInstruction(Opcode.HEAP_GET, AddressingMode.Runtime);
                AddInstruction(Opcode.HEAP_FREE);

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, thereIsReturnValue ? -2 : -1);
                AddInstruction(Opcode.HEAP_FREE);

                if (thereIsReturnValue)
                {
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.StackRelative, -2);
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
            { UndefinedOperatorFunctionOffsets.Add(new UndefinedOffset<CompiledOperator>(jumpInstruction, false, @operator, operatorDefinition, CurrentFile)); }

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (!@operator.SaveValue)
            {
                AddComment(" Clear Return Value:");
                for (int i = 0; i < returnValueSize; i++)
                { AddInstruction(Opcode.POP_VALUE); }
            }

            AddComment("}");
        }
        else if (LanguageOperators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {LanguageOperators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }

            FindStatementType(@operator);

            int jumpInstruction = -1;

            GenerateCodeForStatement(@operator.Left);

            if (opcode == Opcode.LOGIC_AND)
            {
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -1);
                jumpInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JUMP_BY_IF_FALSE);
            }
            else if (opcode == Opcode.LOGIC_OR)
            {
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -1);
                AddInstruction(Opcode.LOGIC_NOT);
                jumpInstruction = GeneratedCode.Count;
                AddInstruction(Opcode.JUMP_BY_IF_FALSE);
            }

            if (@operator.Right != null) GenerateCodeForStatement(@operator.Right);
            AddInstruction(opcode);

            if (jumpInstruction != -1)
            { GeneratedCode[jumpInstruction].Parameter = GeneratedCode.Count - jumpInstruction; }
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
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Integer));

                AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal.GetInt()));
                break;
            }
            case LiteralType.Float:
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Float));

                AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal.GetFloat()));
                break;
            }
            case LiteralType.String:
            {
                GenerateCodeForLiteralString(literal.Value);
                break;
            }
            case LiteralType.Char:
            {
                OnGotStatementType(literal, new BuiltinType(BasicType.Char));

                if (literal.Value.Length != 1) throw new InternalException($"Literal char contains {literal.Value.Length} characters but only 1 allowed", CurrentFile);
                AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal.Value[0]));
                break;
            }
            default: throw new UnreachableException();
        }
    }

    void GenerateCodeForLiteralString(string literal)
    {
        AddComment($"Create String \"{literal}\" {{");

        AddComment("Allocate String object {");

        AddInstruction(Opcode.PUSH_VALUE, 1 + literal.Length);
        AddInstruction(Opcode.HEAP_ALLOC);

        AddComment("}");

        // AddComment("Set String.length {");
        // // Set String.length
        // {
        //     AddInstruction(Opcode.PUSH_VALUE, literal.Length);
        //     AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);
        //     AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
        // }
        // AddComment("}");

        AddComment("Set string data {");

        for (int i = 0; i < literal.Length; i++)
        {
            // Prepare value
            AddInstruction(Opcode.PUSH_VALUE, new DataItem(literal[i]));

            // Calculate pointer
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);
            AddInstruction(Opcode.PUSH_VALUE, i);
            AddInstruction(Opcode.MATH_ADD);

            // Set value
            AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
        }

        {
            // Prepare value
            AddInstruction(Opcode.PUSH_VALUE, new DataItem('\0'));

            // Calculate pointer
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);
            AddInstruction(Opcode.PUSH_VALUE, literal.Length);
            AddInstruction(Opcode.MATH_ADD);

            // Set value
            AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
        }

        AddComment("}");

        AddComment("}");
    }

    void GenerateCodeForStatement(Identifier variable, GeneralType? expectedType = null)
    {
        if (GetConstant(variable.Content, out IConstant? constant))
        {
            OnGotStatementType(variable, constant.Type);
            variable.PredictedValue = constant.Value;
            variable.Reference = constant;

            AddInstruction(Opcode.PUSH_VALUE, constant.Value);
            return;
        }

        if (GetParameter(variable.Content, out CompiledParameter? param))
        {
            if (variable.Content != "this")
            { variable.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }
            variable.Reference = param;
            OnGotStatementType(variable, param.Type);

            ValueAddress address = GetBaseAddress(param);

            AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);

            if (address.IsReference)
            { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.Runtime); }

            return;
        }

        if (GetVariable(variable.Content, out CompiledVariable? val))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = val;
            OnGotStatementType(variable, val.Type);

            StackLoad(new ValueAddress(val), val.Type.Size);
            return;
        }

        if (GetGlobalVariable(variable.Content, out CompiledVariable? globalVariable))
        {
            variable.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = globalVariable;
            OnGotStatementType(variable, globalVariable.Type);

            StackLoad(GetGlobalVariableAddress(globalVariable), globalVariable.Type.Size);
            return;
        }

        if (GetFunction(variable.Token, expectedType, out CompiledFunction? compiledFunction))
        {
            compiledFunction.References.Add((variable, CurrentFile, CurrentContext));
            variable.Token.AnalyzedType = TokenAnalyzedType.FunctionName;
            variable.Reference = compiledFunction;
            OnGotStatementType(variable, new FunctionType(compiledFunction));

            if (compiledFunction.InstructionOffset == -1)
            { UndefinedFunctionOffsets.Add(new UndefinedOffset<CompiledFunction>(GeneratedCode.Count, true, variable, compiledFunction, CurrentFile)); }

            AddInstruction(Opcode.PUSH_VALUE, compiledFunction.InstructionOffset);

            return;
        }

        throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
    }
    void GenerateCodeForStatement(AddressGetter addressGetter)
    {
        if (addressGetter.PrevStatement is Identifier identifier)
        {
            GeneralType type = FindStatementType(identifier);

            throw new CompilerException($"Variable \"{identifier}\" (type of {type}) is stored on the stack", addressGetter, CurrentFile);

            // GenerateCodeForStatement(identifier);
            // return;
        }

        if (addressGetter.PrevStatement is Field field)
        {
            // int offset = GetDataOffset(field);
            // ValueAddress pointerOffset = GetBaseAddress(field);

            throw new CompilerException($"Field \"{field}\" is on the stack", addressGetter, CurrentFile);

            // StackLoad(pointerOffset);
            // AddInstruction(Opcode.MATH_ADD, offset);
            // return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(Pointer memoryAddressFinder)
    {
        GenerateCodeForStatement(memoryAddressFinder.PrevStatement);
        AddInstruction(Opcode.HEAP_GET, AddressingMode.Runtime);
    }
    void GenerateCodeForStatement(WhileLoop whileLoop)
    {
        bool conditionIsComputed = TryCompute(whileLoop.Condition, out DataItem computedCondition);
        if (conditionIsComputed && !(bool)computedCondition && TrimUnreachableCode)
        {
            AddComment("Unreachable code not compiled");
            AnalysisCollection?.Informations.Add(new Information($"Unreachable code not compiled", whileLoop.Block, CurrentFile));
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

        GeneratedCode[conditionJumpOffset].Parameter = GeneratedCode.Count - conditionJumpOffset;

        OnScopeExit();

        AddComment("}");

        if (conditionIsComputed &&
            !(bool)computedCondition)
        { AnalysisCollection?.Warnings.Add(new Warning($"Bruh", whileLoop.Keyword, CurrentFile)); }

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
        GeneratedCode[conditionJumpOffsetFor].Parameter = GeneratedCode.Count - conditionJumpOffsetFor;

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

                GeneratedCode[jumpNextInstruction].Parameter = GeneratedCode.Count - jumpNextInstruction;
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

                GeneratedCode[jumpNextInstruction].Parameter = GeneratedCode.Count - jumpNextInstruction;
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
            GeneratedCode[item].Parameter = GeneratedCode.Count - item;
        }
    }
    void GenerateCodeForStatement(NewInstance newObject)
    {
        AddComment($"new {newObject.TypeName} {{");

        GeneralType instanceType = FindType(newObject.TypeName);

        newObject.TypeName.SetAnalyzedType(instanceType);
        OnGotStatementType(newObject, instanceType);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                AddInstruction(Opcode.PUSH_VALUE, pointerType.To.Size);
                AddInstruction(Opcode.HEAP_ALLOC);

                for (int offset = 0; offset < pointerType.To.Size; offset++)
                {
                    AddInstruction(Opcode.PUSH_VALUE, 0);
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);
                    AddInstruction(Opcode.PUSH_VALUE, offset);
                    AddInstruction(Opcode.MATH_ADD);
                    AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
                }

                break;
            }

            case StructType structType:
            {
                structType.Struct.References.Add((newObject.TypeName, CurrentFile, CurrentContext));

                GenerateInitialValue(structType);
                break;
            }

            default:
                throw new CompilerException($"Unknown type definition {instanceType}", newObject.TypeName, CurrentFile);
        }

        AddComment("}");
    }
    void GenerateCodeForStatement(ConstructorCall constructorCall)
    {
        GeneralType instanceType = GeneralType.From(constructorCall.TypeName, FindType, TryCompute);
        GeneralType[] parameters = FindStatementTypes(constructorCall.Parameters);

        if (!GetConstructor(instanceType, parameters, out CompiledConstructor? compiledFunction))
        {
            if (!GetConstructorTemplate(instanceType, parameters, out CompliableTemplate<CompiledConstructor> compilableFunction))
            { throw new CompilerException($"Constructor {constructorCall.ToReadable(FindStatementType)} not found", constructorCall.TypeName, CurrentFile); }

            compilableFunction = AddCompilable(compilableFunction);
            compiledFunction = compilableFunction.Function;
        }

        compiledFunction.References.Add((constructorCall, CurrentFile, CurrentContext));
        OnGotStatementType(constructorCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new Error($"Constructor {compiledFunction.ToReadable()} could not be called due to its protection level", constructorCall.TypeName, CurrentFile));
            return;
        }

        if (compiledFunction.IsMacro)
        { throw new NotImplementedException(); }

        AddComment($"Call {compiledFunction.ToReadable()} {{");

        Stack<ParameterCleanupItem> parameterCleanup;

        NewInstance newInstance = constructorCall.ToInstantiation();
        GeneralType newInstanceType = FindStatementType(newInstance);
        if (newInstanceType.Size != 1)
        { throw new NotImplementedException(); }
        GenerateCodeForStatement(newInstance);

        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -1);

        parameterCleanup = GenerateCodeForParameterPassing(constructorCall, compiledFunction);
        parameterCleanup.Insert(0, (newInstanceType.Size, false, newInstanceType));

        AddComment(" .:");

        int jumpInstruction = Call(compiledFunction.InstructionOffset);

        if (compiledFunction.InstructionOffset == -1)
        { UndefinedConstructorOffsets.Add(new UndefinedOffset<CompiledConstructor>(jumpInstruction, false, constructorCall, compiledFunction, CurrentFile)); }

        GenerateCodeForParameterCleanup(parameterCleanup);

        AddComment("}");
    }
    void GenerateCodeForStatement(Field field)
    {
        field.FieldName.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType is EnumType enumType)
        {
            if (!enumType.Enum.GetValue(field.FieldName.Content, out DataItem enumValue))
            { throw new CompilerException($"I didn't find anything like \"{field.FieldName.Content}\" in the enum {enumType.Enum.Identifier}", field.FieldName, CurrentFile); }

            OnGotStatementType(field, new BuiltinType(enumValue.Type));

            AddInstruction(Opcode.PUSH_VALUE, enumValue);
            return;
        }

        if (prevType is ArrayType arrayType && field.FieldName.Equals("Length"))
        {
            OnGotStatementType(field, new BuiltinType(BasicType.Integer));
            field.PredictedValue = arrayType.Length;

            AddInstruction(Opcode.PUSH_VALUE, arrayType.Length);
            return;
        }

        if (prevType is PointerType pointerType)
        {
            if (pointerType.To is not StructType structPointerType)
            { throw new CompilerException($"Could not get the field offsets of type {pointerType}", field.PrevStatement, CurrentFile); }

            if (!structPointerType.Struct.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
            { throw new CompilerException($"Could not get the field offset of field \"{field.FieldName}\"", field.FieldName, CurrentFile); }

            GenerateCodeForStatement(field.PrevStatement);

            CheckPointerNull();

            AddInstruction(Opcode.PUSH_VALUE, fieldOffset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_GET, AddressingMode.Runtime);

            return;
        }

        if (prevType is not StructType structType) throw new NotImplementedException();

        GeneralType type = FindStatementType(field);

        if (!structType.Struct.GetField(field.FieldName.Content, out CompiledField? compiledField))
        { throw new CompilerException($"Field definition \"{field.FieldName}\" not found in type \"{structType}\"", field, CurrentFile); }

        field.Reference = compiledField;

        // if (CurrentContext?.Context != null)
        // {
        //     switch (compiledField.Protection)
        //     {
        //         case Protection.Private:
        //             if (CurrentContext.Context.Identifier.Content != compiledField.Class!.Identifier.Content)
        //             { throw new CompilerException($"Can not access field \"{compiledField.Identifier.Content}\" of class \"{compiledField.Class.Identifier}\" due to it's protection level", field, CurrentFile); }
        //             break;
        //         case Protection.Public:
        //             break;
        //         default: throw new UnreachableException();
        //     }
        // }

        if (IsItInHeap(field))
        {
            int offset = GetDataOffset(field);
            ValueAddress pointerOffset = GetBaseAddress(field);
            for (int i = 0; i < type.Size; i++)
            {
                AddComment($"{i}:");
                HeapLoad(pointerOffset, offset + i);
            }
        }
        else
        {
            ValueAddress offset = GetDataAddress(field);
            StackLoad(offset, compiledField.Type.Size);
        }
    }
    void GenerateCodeForStatement(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);

        if (prevType is ArrayType arrayType)
        {
            if (index.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", index.PrevStatement, CurrentFile); }

            if (TryCompute(index.Index, out DataItem computedIndexData))
            {
                index.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || computedIndexData >= arrayType.Length)
                { AnalysisCollection?.Warnings.Add(new Warning($"Index out of range", index.Index, CurrentFile)); }

                if (GetParameter(identifier.Content, out CompiledParameter? param))
                {
                    if (param.Type != arrayType)
                    { throw new NotImplementedException(); }

                    ValueAddress offset = GetBaseAddress(param, (int)computedIndexData * arrayType.Of.Size);
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BasePointerRelative, offset.Address);

                    if (offset.IsReference)
                    { throw new NotImplementedException(); }

                    return;
                }

                if (GetVariable(identifier.Content, out CompiledVariable? val))
                {
                    if (val.Type != arrayType)
                    { throw new NotImplementedException(); }

                    int offset = computedIndexData.ValueSInt32 * arrayType.Of.Size;
                    ValueAddress address = new(val);

                    StackLoad(address + offset, arrayType.Of.Size);

                    return;
                }
            }

            {
                ValueAddress address = GetDataAddress(identifier);
                AddInstruction(Opcode.PUSH_VALUE, address.Address);

                switch (address.AddressingMode)
                {
                    case AddressingMode.Absolute:
                        break;
                    case AddressingMode.Runtime:
                        throw new NotImplementedException();
                    case AddressingMode.BasePointerRelative:
                        AddInstruction(Opcode.GET_BASEPOINTER);
                        AddInstruction(Opcode.MATH_ADD);
                        break;
                    case AddressingMode.StackRelative:
                        throw new UnreachableException();
                    default:
                        throw new UnreachableException();
                }

                GenerateCodeForStatement(index.Index);
                AddInstruction(Opcode.MATH_ADD);

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.Runtime);
                return;
            }

            throw new NotImplementedException();
        }

        if (!GetIndexGetter(prevType, out CompiledFunction? indexer))
        {
            if (GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
            {
                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }
        }

        if (indexer == null && prevType is PointerType pointerType)
        {
            GenerateCodeForStatement(index.PrevStatement);

            AddInstruction(Opcode.PUSH_VALUE, pointerType.To.Size);
            GeneralType indexType = FindStatementType(index.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", index.Index, CurrentFile); }
            GenerateCodeForStatement(index.Index);
            AddInstruction(Opcode.MATH_MULT);

            AddInstruction(Opcode.MATH_ADD);

            CheckPointerNull();

            AddInstruction(Opcode.HEAP_GET, AddressingMode.Runtime);

            return;
        }

        if (indexer == null)
        { throw new CompilerException($"Index getter \"{prevType}[]\" not found", index, CurrentFile); }

        GenerateCodeForFunctionCall_Function(new FunctionCall(
                index.PrevStatement,
                Token.CreateAnonymous(BuiltinFunctionNames.IndexerGet),
                index.BracketLeft,
                new StatementWithValue[]
                {
                    index.Index,
                },
                index.BracketRight
            ), indexer);
    }
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals("ref"))
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

                switch (address.AddressingMode)
                {
                    case AddressingMode.Absolute:
                        break;
                    case AddressingMode.Runtime:
                        throw new NotImplementedException();
                    case AddressingMode.BasePointerRelative:
                        AddInstruction(Opcode.GET_BASEPOINTER);
                        AddInstruction(Opcode.MATH_ADD);
                        break;
                    case AddressingMode.StackRelative:
                        throw new UnreachableException();
                    default:
                        throw new UnreachableException();
                }
            }
            return;
        }

        if (modifier.Equals("temp"))
        {
            GenerateCodeForStatement(statement);
            return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(LiteralList listValue)
    { throw new NotImplementedException(); }
    void GenerateCodeForStatement(TypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (statementType.Size != targetType.Size)
        { throw new CompilerException($"Can't modify the size of the value. You tried to convert from {statementType} (size of {statementType.Size}) to {targetType} (size of {targetType.Size})", new Position(typeCast.Keyword, typeCast.Type), CurrentFile); }

        GenerateCodeForStatement(typeCast.PrevStatement, targetType);

        GeneralType type = FindStatementType(typeCast.PrevStatement, targetType);

        if (targetType is not FunctionType && type == targetType)
        {
            AnalysisCollection?.Hints.Add(new Hint($"Redundant type conversion", typeCast.Keyword, CurrentFile));
            return;
        }

        if (type is BuiltinType && targetType is BuiltinType targetBuiltinType)
        {
            AddInstruction(Opcode.PUSH_VALUE, new DataItem((byte)targetBuiltinType.Type.Convert()));
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

    void GenerateCodeForStatement(Statement statement, GeneralType? expectedType = null)
    {
        int startInstruction = GeneratedCode.Count;

        switch (statement)
        {
            case LiteralList v: GenerateCodeForStatement(v); break;
            case VariableDeclaration v: GenerateCodeForStatement(v); break;
            case FunctionCall v: GenerateCodeForStatement(v); break;
            case KeywordCall v: GenerateCodeForStatement(v); break;
            case OperatorCall v: GenerateCodeForStatement(v); break;
            case AnyAssignment v: GenerateCodeForStatement(v.ToAssignment()); break;
            case LiteralStatement v: GenerateCodeForStatement(v); break;
            case Identifier v: GenerateCodeForStatement(v, expectedType); break;
            case AddressGetter v: GenerateCodeForStatement(v); break;
            case Pointer v: GenerateCodeForStatement(v); break;
            case WhileLoop v: GenerateCodeForStatement(v); break;
            case ForLoop v: GenerateCodeForStatement(v); break;
            case IfContainer v: GenerateCodeForStatement(v); break;
            case NewInstance v: GenerateCodeForStatement(v); break;
            case ConstructorCall v: GenerateCodeForStatement(v); break;
            case IndexCall v: GenerateCodeForStatement(v); break;
            case Field v: GenerateCodeForStatement(v); break;
            case TypeCast v: GenerateCodeForStatement(v); break;
            case ModifiedStatement v: GenerateCodeForStatement(v); break;
            case AnyCall v: GenerateCodeForStatement(v); break;
            case Block v: GenerateCodeForStatement(v); break;
            default: throw new InternalException($"Unimplemented statement {statement.GetType().Name}");
        }

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
        switch (statementToSet)
        {
            case Identifier v: GenerateCodeForValueSetter(v, value); break;
            case Field v: GenerateCodeForValueSetter(v, value); break;
            case IndexCall v: GenerateCodeForValueSetter(v, value); break;
            case Pointer v: GenerateCodeForValueSetter(v, value); break;
            default: throw new CompilerException($"The left side of the assignment operator should be a variable, field or memory address. Passed {statementToSet.GetType().Name}", statementToSet, CurrentFile);
        }
    }
    void GenerateCodeForValueSetter(Identifier statementToSet, StatementWithValue value)
    {
        if (GetConstant(statementToSet.Content, out _))
        { throw new CompilerException($"Can not set constant value: it is readonly", statementToSet, CurrentFile); }

        if (GetParameter(statementToSet.Content, out CompiledParameter? parameter))
        {
            if (statementToSet.Content != "this")
            { statementToSet.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }

            GeneralType valueType = FindStatementType(value, parameter.Type);

            AssignTypeCheck(parameter.Type, valueType, value);

            GenerateCodeForStatement(value);

            ValueAddress address = GetBaseAddress(parameter);

            if (address.IsReference)
            {
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BasePointerRelative, address.Address);
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.Runtime);
            }
            else
            {
                StackStore(address);
            }
        }
        else if (GetVariable(statementToSet.Content, out CompiledVariable? variable))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;

            GeneralType valueType = FindStatementType(value, variable.Type);

            AssignTypeCheck(variable.Type, valueType, value);

            GenerateCodeForStatement(value);

            StackStore(new ValueAddress(variable));
        }
        else if (GetGlobalVariable(statementToSet.Content, out CompiledVariable? globalVariable))
        {
            statementToSet.Token.AnalyzedType = TokenAnalyzedType.VariableName;

            GeneralType valueType = FindStatementType(value, globalVariable.Type);

            AssignTypeCheck(globalVariable.Type, valueType, value);

            GenerateCodeForStatement(value);

            StackStore(GetGlobalVariableAddress(globalVariable));
        }
        else
        {
            throw new CompilerException($"Symbol \"{statementToSet.Content}\" not found", statementToSet, CurrentFile);
        }
    }
    void GenerateCodeForValueSetter(Field statementToSet, StatementWithValue value)
    {
        statementToSet.FieldName.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType valueType = FindStatementType(value);

        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);

        if (prevType is PointerType pointerType)
        {
            if (pointerType.To is not StructType structType)
            { throw new CompilerException($"Failed to get the field offsets of type {pointerType.To}", statementToSet.PrevStatement, CurrentFile); }

            if (!structType.Struct.FieldOffsets.TryGetValue(statementToSet.FieldName.Content, out int fieldOffset))
            { throw new CompilerException($"Failed to get the field offset for \"{statementToSet.FieldName}\" in type {pointerType.To}", statementToSet.FieldName, CurrentFile); }

            GenerateCodeForStatement(statementToSet.PrevStatement);

            GenerateCodeForStatement(value);
            ValueAddress pointerAddress = new(-valueType.Size - 1, AddressingMode.StackRelative);
            HeapStore(pointerAddress, fieldOffset);

            AddInstruction(Opcode.POP_VALUE);
            return;
        }

        if (prevType is not StructType structType1)
        { throw new NotImplementedException(); }

        GeneralType type = FindStatementType(statementToSet);

        if (type != valueType)
        { throw new CompilerException($"Can not set a \"{valueType}\" type value to the \"{type}\" type field.", value, CurrentFile); }

        structType1.Struct.AddTypeArguments(structType1.TypeParameters);

        GenerateCodeForStatement(value);

        bool _inHeap = IsItInHeap(statementToSet);

        structType1.Struct.ClearTypeArguments();

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
            StackStore(offset, valueType.Size);

            return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForValueSetter(IndexCall statementToSet, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType valueType = FindStatementType(value);

        if (prevType is ArrayType arrayType)
        {
            if (statementToSet.PrevStatement is not Identifier identifier)
            { throw new NotSupportedException($"Only variables/parameters supported by now", statementToSet.PrevStatement, CurrentFile); }

            if (TryCompute(statementToSet.Index, out DataItem computedIndexData))
            {
                statementToSet.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || computedIndexData >= arrayType.Length)
                { AnalysisCollection?.Warnings.Add(new Warning($"Index out of range", statementToSet.Index, CurrentFile)); }

                GenerateCodeForStatement(value);

                if (GetParameter(identifier.Content, out _))
                { throw new NotImplementedException(); }

                if (GetVariable(identifier.Content, out CompiledVariable? variable))
                {
                    if (variable.Type != arrayType)
                    { throw new NotImplementedException(); }

                    int offset = (int)computedIndexData * arrayType.Of.Size;
                    StackStore(new ValueAddress(variable) + offset, arrayType.Of.Size);
                    return;
                }
            }

            throw new NotImplementedException();
        }

        if (!GetIndexSetter(prevType, valueType, out CompiledFunction? indexer))
        {
            if (GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
            {
                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }
        }

        if (indexer == null && prevType is PointerType pointerType)
        {
            AssignTypeCheck(pointerType.To, valueType, value);

            GenerateCodeForStatement(value);

            GenerateCodeForStatement(statementToSet.PrevStatement);

            AddInstruction(Opcode.PUSH_VALUE, pointerType.To.Size);
            GeneralType indexType = FindStatementType(statementToSet.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", statementToSet.Index, CurrentFile); }
            GenerateCodeForStatement(statementToSet.Index);
            AddInstruction(Opcode.MATH_MULT);

            AddInstruction(Opcode.MATH_ADD);

            CheckPointerNull();

            AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);

            return;
        }

        if (indexer != null)
        {
            GenerateCodeForFunctionCall_Function(new FunctionCall(
                    statementToSet.PrevStatement,
                    Token.CreateAnonymous(BuiltinFunctionNames.IndexerSet),
                    statementToSet.BracketLeft,
                    new StatementWithValue[]
                    {
                    statementToSet.Index,
                    value,
                    },
                    statementToSet.BracketRight
                ), indexer);
            return;
        }

        throw new CompilerException($"Index setter \"{prevType}[...] = {valueType}\" not found", statementToSet, CurrentFile);
    }
    void GenerateCodeForValueSetter(Pointer statementToSet, StatementWithValue value)
    {
        GeneralType targetType = FindStatementType(statementToSet);

        if (targetType.Size != 1) throw new NotImplementedException();

        GenerateCodeForStatement(value);
        GenerateCodeForStatement(statementToSet.PrevStatement);

        AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
    }
    void GenerateCodeForValueSetter(VariableDeclaration statementToSet, StatementWithValue value)
    {
        if (GetConstant(statementToSet.VariableName.Content, out _))
        { throw new CompilerException($"Can not set constant value: it is readonly", statementToSet, statementToSet.FilePath); }

        if (value is LiteralList)
        { throw new NotImplementedException(); }

        if (!GetVariable(statementToSet.VariableName.Content, out CompiledVariable? variable))
        { throw new CompilerException($"Variable \"{statementToSet.VariableName.Content}\" not found", statementToSet.VariableName, CurrentFile); }

        GeneralType valueType = FindStatementType(value);

        AssignTypeCheck(variable.Type, valueType, value);

        if (variable.Type is BuiltinType &&
            TryCompute(value, out DataItem yeah))
        {
            value.PredictedValue = yeah;
            AddInstruction(Opcode.PUSH_VALUE, yeah);
        }
        else if (variable.Type is ArrayType arrayType)
        {
            if (arrayType.Of != BasicType.Char)
            { throw new InternalException(); }
            if (value is not LiteralStatement literal)
            { throw new InternalException(); }
            if (literal.Type != LiteralType.String)
            { throw new InternalException(); }
            if (literal.Value.Length != arrayType.Length)
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

        int destination = variable.MemoryAddress;
        int size = variable.Type.Size;
        for (int offset = 1; offset <= size; offset++)
        { AddInstruction(Opcode.STORE_VALUE, AddressingMode.BasePointerRelative, destination + size - offset); }
    }

    void GenerateDeallocator(GeneralType deallocateableType)
    {
        AddComment($"Deallocate \"{deallocateableType}\" {{");

        if (deallocateableType is PointerType)
        {
            AddInstruction(Opcode.HEAP_FREE);
            AddComment("}");
            return;
        }

        /*
        if (deallocateableType.IsClass)
        {
            if (!GetGeneralFunction(deallocateableType, new GeneralType[] { deallocateableType }, BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
            {
                if (!GetGeneralFunctionTemplate(deallocateableType, new GeneralType[] { deallocateableType }, BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> destructorTemplate))
                {
                    AddInstruction(Opcode.HEAP_FREE);
                    AddComment("}");
                    return;
                }
                destructorTemplate = AddCompilable(destructorTemplate);
                destructor = destructorTemplate.Function;
            }

            if (!destructor.CanUse(CurrentFile))
            {
                AnalysisCollection?.Errors.Add(new Error($"Destructor for type '{deallocateableType.Class.Identifier.Content}' function cannot be called due to its protection level", null, CurrentFile));
                AddComment("}");
                return;
            }

            AddComment(" Param0 (should be already be there):");

            AddComment(" .:");

            int jumpInstruction = Call(destructor.InstructionOffset);

            if (destructor.InstructionOffset == -1)
            { UndefinedGeneralFunctionOffsets.Add(new UndefinedOffset<CompiledGeneralFunction>(jumpInstruction, false, null, destructor, CurrentFile)); }

            AddComment(" Clear Param:");

            AddInstruction(Opcode.POP_VALUE);

            AddComment("}");
            return;
        }

        AddInstruction(Opcode.HEAP_FREE);
        AddComment("}");
        */
        throw new NotImplementedException();
    }

    void GenerateCodeForInlinedMacro(Statement inlinedMacro)
    {
        InMacro.Push(true);
        if (inlinedMacro is Block block)
        { GenerateCodeForStatement(block); }
        else if (inlinedMacro is KeywordCall keywordCall &&
            keywordCall.Identifier.Equals("return") &&
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

        CleanupStack.Push(CompileVariables(block, CurrentContext is null));
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
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BasePointerRelative, ReturnFlagOffset);
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
                AnalysisCollection?.Warnings.Add(new Warning($"Variable \"{CompiledVariables[i].VariableName}\" already defined", CompiledVariables[i].VariableName, CurrentFile));
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
            Size = compiledVariable.Type.Size,
        };

        if (compiledVariable.Type is PointerType)
        { debugInfo.Type = StackElementType.HeapPointer; }
        else
        { debugInfo.Type = StackElementType.Value; }

        CurrentScopeDebug.Last.Stack.Add(debugInfo);

        CompiledVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        int size;

        if (TryCompute(newVariable.InitialValue, out DataItem computedInitialValue))
        {
            newVariable.InitialValue.PredictedValue = computedInitialValue;

            AddComment($"Initial value {{");

            size = 1;

            AddInstruction(Opcode.PUSH_VALUE, computedInitialValue);
            compiledVariable.IsInitialized = true;

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }

            AddComment("}");
        }
        else if (compiledVariable.Type is ArrayType arrayType &&
            arrayType.Of == BasicType.Char &&
            newVariable.InitialValue is LiteralStatement literalStatement &&
            literalStatement.Type == LiteralType.String &&
            literalStatement.Value.Length == arrayType.Length)
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

        if (size != compiledVariable.Type.Size)
        { throw new InternalException($"Variable size ({compiledVariable.Type.Size}) and initial value size ({size}) mismatch"); }

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
            { sum += CompiledVariables[i].Type.Size; }
            return sum;
        }
    }

    void FinishJumpInstructions(IEnumerable<int> jumpInstructions)
        => FinishJumpInstructions(jumpInstructions, GeneratedCode.Count);
    void FinishJumpInstructions(IEnumerable<int> jumpInstructions, int jumpTo)
    {
        foreach (int jumpInstruction in jumpInstructions)
        {
            GeneratedCode[jumpInstruction].Parameter = jumpTo - jumpInstruction;
        }
    }

    void GenerateCodeForFunction(FunctionThingDefinition function)
    {
        if (LanguageConstants.Keywords.Contains(function.Identifier?.ToString()))
        { throw new CompilerException($"The identifier \"{function.Identifier}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.FilePath); }

        if (function.Identifier is not null)
        { function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName; }

        if (function is FunctionDefinition functionDefinition)
        {
            for (int i = 0; i < functionDefinition.Attributes.Length; i++)
            {
                if (functionDefinition.Attributes[i].Identifier.Equals("External"))
                { return; }
            }
        }

        if (function.Identifier is not null)
        { AddComment(function.Identifier.Content + ((function.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }")); }
        else
        { AddComment("null" + ((function.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }")); }

        CurrentContext = function as ISameCheck;
        InFunction = true;

        TagCount.Push(0);
        InMacro.Push(false);

        CompiledParameters.Clear();
        CompiledVariables.Clear();
        ReturnInstructions.Clear();

        CompileParameters(function.Parameters.ToArray());

        CurrentFile = function.FilePath;

        int instructionStart = GeneratedCode.Count;

        CanReturn = true;
        AddInstruction(Opcode.PUSH_VALUE, new DataItem(false));
        TagCount.Last++;

        OnScopeEnter(function.Block ?? throw new CompilerException($"Function \"{function.ToReadable()}\" does not have a body"));

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
                Size = p.Type.Size,
                Tag = p.Identifier.Content,
            };
            if (p.IsRef)
            { debugInfo.Type = StackElementType.StackPointer; }
            else if (p.Type is PointerType)
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

        if (function.Block != null && function.Block.Statements.Length > 0) AddComment("}");

        if (function.Identifier is not null)
        {
            GeneratedDebugInfo.FunctionInformations.Add(new FunctionInformations()
            {
                IsValid = true,
                IsMacro = false,
                SourcePosition = function.Identifier.Position,
                Identifier = function.Identifier.Content,
                File = function.FilePath,
                ReadableIdentifier = function.ToReadable(),
                Instructions = (instructionStart, GeneratedCode.Count),
            });
        }

        CompiledParameters.Clear();
        CompiledVariables.Clear();
        ReturnInstructions.Clear();

        InMacro.Pop();
        TagCount.Pop();

        CurrentContext = null;
        InFunction = false;
    }

    void GenerateCodeForCompilableFunction<T>(CompliableTemplate<T> function)
        where T : FunctionThingDefinition, ITemplateable<T>
    {
        SetTypeArguments(function.TypeArguments);

        GenerateCodeForFunction(function.Function);

        TypeArguments.Clear();
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

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = 0,
            BasepointerRelative = false,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Exit Code",
            Type = StackElementType.Value,
        });

        AddInstruction(Opcode.GET_BASEPOINTER);
        AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.StackRelative, 0);

        CurrentScopeDebug.Last.Stack.Add(new StackElementInformations()
        {
            Address = SavedBasePointerOffset,
            BasepointerRelative = true,
            Kind = StackElementKind.Internal,
            Size = 1,
            Tag = "Saved BasePointer",
            Type = StackElementType.Value,
        });

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

        CompiledGlobalVariables.AddRange(CompiledVariables);
        CleanupVariables(CleanupStack.Pop());

        CleanupConstants();

        CanReturn = false;
        AddInstruction(Opcode.POP_VALUE);
        TagCount.Last--;

        InMacro.Pop();
        TagCount.Pop();

        AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.Runtime, 0);

        AddComment("}");

        ScopeInformations scope = CurrentScopeDebug.Pop();
        scope.Location.Instructions.End = GeneratedCode.Count - 1;
        GeneratedDebugInfo.ScopeInformations.Add(scope);
    }

    void CompileParameters(ParameterDefinition[] parameters)
    {
        int paramsSize = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            GeneralType parameterType = GeneralType.From(parameters[i].Type, FindType);
            parameters[i].Type.SetAnalyzedType(parameterType);

            this.CompiledParameters.Add(new CompiledParameter(i, -(paramsSize + 1 + CodeGeneratorForMain.TagsBeforeBasePointer), parameterType, parameters[i]));

            paramsSize += parameterType.Size;
        }
    }

    #endregion
}